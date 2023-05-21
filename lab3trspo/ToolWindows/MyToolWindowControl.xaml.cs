using EnvDTE;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;

namespace lab3trspo
{

    public class FunctionStatistics
    {
        public List<string> Name { get; set; }
        public List<int> LineCount { get; set; }
        public List<int> NonEmptyLineCount { get; set; }
        public List<int> KeywordCount { get; set; }

        public FunctionStatistics()
        {
            Name = new List<string>();
            LineCount = new List<int>();
            NonEmptyLineCount = new List<int>();
            KeywordCount = new List<int>();
        }

        public void ClearData()
        {
            Name.Clear();
            LineCount.Clear();
            NonEmptyLineCount.Clear();
            KeywordCount.Clear();
        }
    }


    // ---------------------------------------------------
    public class ActiveFile
    {
        public int countWords = 0;
        public string ActiveText { get; set; } = "";
        public string[] lines;
        public ActiveFile() { }
    }

    // ---------------------------------------------------
    public class Parser
    {
        public FunctionStatistics FuncStat = new();
        private readonly string[] KeyWords =
        {
            "alignas", "alignof", "&&", "__asm", "auto", "&", "^", "bool", "break",
            "case", "catch", "char","char16_t", "char32_t", "class", "~", "const", "constexpr", "const_cast",
            "continue", "decltype", "default", "delete", "do", "double", "dynamic_cast", "else",
            "enum", "explicit", "export", "extern", "false", "float", "for", "friend", "goto", "if",
            "int", "long", "mutable", "new", "noexcept", "!", "!=", "nullptr", "static_cast",
            "operator", "||", "==", "private", "protected", "public","register", "reinterpret_cast",
            "return", "short", "signed", "sizeof", "static", "static_assert", "static_cast", "struct",
            "switch", "template", "this", "thread_local", "throw", "true", "try", "typedef", "typeid",
            "typename", "union", "unsigned", "virtual", "void", "volatile", "wchar_t", "while",
            "xor", "xor_eq", "*=", "-=", "++", "--", "/=", "+="
        };
        public Parser() { }
        public string GetActiveFile()
        {
            string text;
            ThreadHelper.ThrowIfNotOnUIThread();
            DTE dte = Package.GetGlobalService(typeof(DTE)) as DTE;
            Document doc = dte.ActiveDocument;
            if (doc != null)
            {

                if (doc.Object("TextDocument") is TextDocument textDoc)
                {
                    EditPoint topPoint = textDoc.StartPoint.CreateEditPoint();
                    text = topPoint.GetText(textDoc.EndPoint);
                    Console.WriteLine(text);
                    //VS.MessageBox.Show("Statistics", text);
                    return text;
                }
            }
            else
            {
                Console.WriteLine("No active document.");
            }
            return null;
        }


        public void Parse(string text)
        {
            string keywordPattern = @"\b(" + string.Join("|", KeyWords.Select(Regex.Escape)) + @")\b";
            string functionPattern = @"(?<=\b(?:" + keywordPattern + @")\s+)\b\w+\b\s*\([^)]*\)\s*{[^}]*}";

            Stack<char> bracesStack = new();
            StringBuilder currentFunction = new();

            foreach (char c in text)
            {
                if (c == '{')
                {
                    bracesStack.Push(c);
                }
                else if (c == '}')
                {
                    bracesStack.Pop();
                    if (bracesStack.Count == 0)
                    {
                        // Мы достигли конца функции, обрабатываем ее содержимое
                        string functionText = currentFunction.ToString();

                        Match functionNameMatch = Regex.Match(functionText, @"(?<=\b(?:" + keywordPattern + @")\s+)\b\w+\b");
                        if (functionNameMatch.Success)
                        {
                            string functionName = functionNameMatch.Value;
                            int lineCount = Regex.Matches(functionText, @"[\r\n]+").Count + 1;

                            // Исключение слов в кавычках из поиска ключевых слов
                            MatchCollection stringMatches = Regex.Matches(functionText, @"\""(.*?)\""", RegexOptions.Singleline);
                            foreach (Match stringMatch in stringMatches)
                            {
                                string stringText = stringMatch.Groups[1].Value;
                                functionText = functionText.Replace(stringMatch.Value, new string(' ', stringText.Length));
                            }

                            MatchCollection keywordMatches = Regex.Matches(functionText, keywordPattern);
                            int keywordCount = keywordMatches.Count;

                            int emptyLineCount = 0;
                            int nonEmptyLineCount = 0;

                            using (StringReader reader = new(functionText))
                            {
                                string line;
                                while ((line = reader.ReadLine()) != null)
                                {
                                    if (string.IsNullOrWhiteSpace(line))
                                        emptyLineCount++;
                                    else if (!line.TrimStart().StartsWith("//"))
                                        nonEmptyLineCount++;
                                }
                            }

                            // Проверка последней строки на комментарий после закрывающей скобки
                            string lastLine = functionText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.TrimEnd();
                            if (!string.IsNullOrEmpty(lastLine) && lastLine.StartsWith("//"))
                            {
                                emptyLineCount--;
                                nonEmptyLineCount++;
                            }

                            FuncStat.Name.Add(functionName);
                            FuncStat.LineCount.Add(lineCount);
                            FuncStat.NonEmptyLineCount.Add(nonEmptyLineCount);
                            FuncStat.KeywordCount.Add(keywordCount);

                            if (keywordCount > 0)
                            {
                                List<string> keywords = keywordMatches.Cast<Match>().Select(m => m.Value).ToList();
                                string keywordMessage = string.Format("Function: {0}\nKeywords: {1}", functionName, string.Join(", ", keywords));
                                //VS.MessageBox.Show("Keywords Found", keywordMessage);
                            }
                        }

                        // Сброс текущей функции
                        currentFunction.Clear();
                    }
                }
                // Добавляем символ к текущей функции
                currentFunction.Append(c);
            }
        }

    }
    // ---------------------------------------------------


    public partial class MyToolWindowControl : UserControl
    {
        public ActiveFile CurrentFile = new();
        public Parser parser = new();

        public MyToolWindowControl()
        {
            InitializeComponent(); 
            KeyUp += MyToolWindowControl_KeyUp;
        }

        private void PreClearData()
        {
            Stats.Items.Clear();
            parser.FuncStat.ClearData();
        }

        private void AddRowStats(string func, int lines, int NELines, int Key)
        {
            Stats.Items.Add(new { Name = func, 
                                  Lines = lines, 
                                  NonEmptyLines = NELines, 
                                  Keywords = Key });
        }

        private void AddingRowToStats()
        {
            for (int i = 0; i < parser.FuncStat.Name.Count; i++)
            {
                AddRowStats(parser.FuncStat.Name[i], 
                            parser.FuncStat.LineCount[i], 
                            parser.FuncStat.NonEmptyLineCount[i], 
                            parser.FuncStat.KeywordCount[i]);
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            PreClearData();
            CurrentFile.ActiveText = parser.GetActiveFile();
            parser.Parse(CurrentFile.ActiveText);
            AddingRowToStats();
        }

        private void MyToolWindowControl_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyboardDevice.Modifiers == ModifierKeys.Alt || e.Key == Key.LeftCtrl)
            {
                RefreshButton_Click(sender, e);
            }
        }
        private void InfoButton_Click(object sender, RoutedEventArgs e)
        {
            VS.MessageBox.Show("Developed by Arthur Khasanov aka thurrsense. 2023.", 
                               "Click 'Refresh' to see statistics about file: \n " +
                               "- Name of function\n - Number of lines in this function\n " +
                               "- Number of lines without comms and space\n " +
                               "- Number of keywords\n " +
                               "Shortcut to refresh window Ctrl+Alt.");
        }
    }
}