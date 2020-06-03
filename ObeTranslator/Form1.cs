using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ObeTranslator
{
    public partial class Form1 : Form
    {
        public string FilePath { get; set; }
        public List<string> Words { get; set; }
        Regex SearchRegex = new Regex("<span class=\"def\" hclass=\"def\" htag=\"span\">(.*?)(</span>|<a)", RegexOptions.IgnoreCase);
        Regex SearchRegex2 = new Regex("<span class=\"def\" (.*?)(</span>|<a)", RegexOptions.IgnoreCase);
        Regex SpanRegex = new Regex("<span class=\"def\" hclass=\"def\" htag=\"span\">", RegexOptions.IgnoreCase);
        Regex SpanEndRegex = new Regex("<(.*?)>", RegexOptions.IgnoreCase);
        Regex CommaRegex = new Regex(",", RegexOptions.IgnoreCase);

        private static readonly HttpClient client = new HttpClient();
        private static readonly HttpClient Postclient = new HttpClient();
        public Form1()
        {
            InitializeComponent();
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {

            if (dialogFilePath.ShowDialog() == DialogResult.OK)
            {
                txtFilePath.Text = FilePath = dialogFilePath.FileName;
                Log("Getting Text file...");
                using (var file = new System.IO.StreamReader(FilePath))
                {
                    Log("Reading the file...");
                    string word = string.Empty;
                    Words = new List<string>();
                    while ((word = file.ReadLine()) != null)
                    {
                        if (string.IsNullOrEmpty(word) || string.IsNullOrWhiteSpace(word))
                            continue;
                        Words.Add(word.Trim());
                    }
                    Words = Words.Distinct().ToList();
                    Log($"File Readed with ({Words.Count}) words");
                }
            }
            else
            {
                Error("Browse File: Can't Read The file ...");
            }
        }

        private void Error(string error)
        {
            Log(error);
            MessageBox.Show(error, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void Log(string error)
        {
            MethodInvoker(() => txtLog.Text += "\n" + error, txtLog);
        }
        private void IncreaseCount(int count)
        {
            MethodInvoker(() => lblCount.Text = $"{count} of {Words.Count}", lblCount);
        }
        private void ProgreesIncrease()
        {
            MethodInvoker(() => progressBar1.Value += 1, progressBar1);
        }
        private void ProgreesReset(int max)
        {
            MethodInvoker(() =>
            {
                progressBar1.Minimum = 0;
                progressBar1.Maximum = max;
            }, progressBar1);
        }
        private void MethodInvoker(Action method, Control control)
        {
            MethodInvoker mi = new MethodInvoker(method);
            if (control.InvokeRequired)
            {
                control.Invoke(mi);
            }
            else
            {
                mi.Invoke();
            }
        }

        private void btnTranslate_Click(object sender, EventArgs e)
        {
            StringBuilder result = new StringBuilder();
            result.Append("The word,English Meaning 1,English Meaning 2,Arabic Meaning\n");
            ProgreesReset(Words.Count);
            Log("**** Start Translate ....");
            Postclient.BaseAddress = new Uri("https://www.translate.com");
            int count = 0;
            foreach (var word in Words)
            {
                Log("Translate " + word);
                string translatedWord = word;
                string line = word;
                if (word.IndexOf(',') > -1)
                {
                    translatedWord = word.Substring(0, word.IndexOf(','));
                }

                if (ckbEnglishMeaning.Checked)
                {
                    Log("Get English meaning for " + translatedWord);
                    line += "," + GetEnglishMeaning(translatedWord);
                }
                Log("Get Arabic meaning for " + translatedWord);
                line += "," + GetArabicMeaning(translatedWord);
                result.Append(line + "\n");
                ProgreesIncrease();
                Log($"({translatedWord}) Added to list");
                Thread.Sleep(50);
                count++;
                IncreaseCount(count);
            }
            var fileName = Path.GetFileNameWithoutExtension(FilePath);
            var newFilePath = FilePath.Replace(fileName + ".txt", fileName + "_New.csv");
            if (File.Exists(newFilePath))
            {
                File.Delete(newFilePath);
            }
            Log("Saving file with path " + newFilePath);
            using (var sr = new StreamWriter(newFilePath, true, System.Text.Encoding.UTF8))
            {
                sr.Write(result.ToString());
            }
            Log("******* DONE ********");
            MessageBox.Show("Done");
        }

        private string GetArabicMeaning(string word)
        {
            try
            {
                var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("text_to_translate", word),
                new KeyValuePair<string, string>("source_lang", "en"),
                new KeyValuePair<string, string>("translated_lang", "ar"),
                new KeyValuePair<string, string>("use_cache_only", "false"),
            });

                var result = Postclient.PostAsync("/translator/ajax_translate", content).Result;
                if (result.IsSuccessStatusCode)
                {
                    string resultContent = result.Content.ReadAsStringAsync().Result;
                    TranslateArabic resultText = JsonConvert.DeserializeObject<TranslateArabic>(resultContent);
                    if (resultText.result == "success")
                    {
                        return resultText.translated_text;
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"ERROR ON WORD: {word} |{ex.Message}");
            }
            return "ERROR";
        }

        private string GetEnglishMeaning(string word)
        {
            try
            {
                var result = new List<string>();

                var responseString = client.GetStringAsync($"https://www.oxfordlearnersdictionaries.com/definition/english/" + word + "_1?q=" + word).Result;
                MatchCollection means = null;

                if (SearchRegex.IsMatch(responseString))
                {
                    means = SearchRegex.Matches(responseString);
                }
                else if (SearchRegex2.IsMatch(responseString))
                {
                    means = SearchRegex2.Matches(responseString);
                }
                else  
                {
                    responseString = client.GetStringAsync($"https://www.oxfordlearnersdictionaries.com/definition/academic/" + word).Result;
                    if (SearchRegex.IsMatch(responseString))
                    {
                        means = SearchRegex.Matches(responseString);
                    }
                    else if (SearchRegex2.IsMatch(responseString))
                    {
                        means = SearchRegex2.Matches(responseString);
                    }
                }
                int maxCount = 2;
                int count = 0;
                foreach (var item in means)
                {
                    if (maxCount == count)
                    {
                        break;
                    }
                    count++;
                    result.Add(SpanRegex.Replace(SpanEndRegex.Replace(CommaRegex.Replace(item.ToString(), ";"), ""), "").Replace("<span class=\"dh\">","").Replace("<a",""));
                }
                if (result.Count == 0)
                {
                    result.Add("");
                }
                if (result.Count == 1)
                {
                    result.Add("");
                }
                return string.Join(",", result);
            }
            catch (Exception ex)
            {
                Log($"ERROR ON WORD: {word} |{ex.Message}");
            }
            return "ERROR";
        }

        private void txtLog_TextChanged(object sender, EventArgs e)
        {
            // set the current caret position to the end
            txtLog.SelectionStart = txtLog.Text.Length;
            // scroll it automatically
            txtLog.ScrollToCaret();
        }
    }
    public class TranslateArabic
    {
        public string result { get; set; }
        public string translated_text { get; set; }
    }
}
