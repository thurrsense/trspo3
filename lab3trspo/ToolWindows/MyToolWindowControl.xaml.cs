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
            // Паттерн для поиска функций
            string functionPattern = @"(int|void|char|double|float|long|short|unsigned|signed|bool|wchar_t|auto)\s+(\w+)\s*\([^)]*\)\s*{(?:[^{}]*{[^{}]*})*[^{}]*}";

            // Паттерн для поиска строк в функции
            string stringLiteralPattern = @"""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])*'";

            // Паттерн для поиска ключевых слов
            string keywordPattern = @"\b(?:";
            keywordPattern += string.Join("|", KeyWords.Select(Regex.Escape));
            keywordPattern += @")\b";

            // Замена продолжающихся комментариев на однострочные комментарии
            text = Regex.Replace(text, @"//[^\r\n]*\\(?:\r?\n|$)", match =>
            {
                string matchedText = match.Value;
                int slashIndex = matchedText.IndexOf("//");
                string commentText = matchedText.Substring(slashIndex + 2).TrimStart();

                string[] lines = commentText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                string[] commentLines = lines.Select(line => $"// {line.TrimStart()}").ToArray();

                return string.Join(Environment.NewLine, commentLines);
            });

            // Начало цикла сборки статистики
            MatchCollection functionMatches = Regex.Matches(text, functionPattern, RegexOptions.Singleline);
            foreach (Match functionMatch in functionMatches)
            {
                string returnType = functionMatch.Groups[1].Value;
                string functionName = functionMatch.Groups[2].Value;
                string functionBody = functionMatch.Value;

                // Удаляем многострочные комментарии (/* ... */)
                functionBody = Regex.Replace(functionBody, @"/\*.*?\*/", "", RegexOptions.Singleline | RegexOptions.Singleline);

                // Удаляем однострочные комментарии (// ...)
                functionBody = Regex.Replace(functionBody, @"//[^\r\n]*", "");

                // Удаляем строки в функции
                functionBody = Regex.Replace(functionBody, stringLiteralPattern, "");

                // Удаляем строки с обратным слешем в функции
                functionBody = Regex.Replace(functionBody, @"\\\r?\n", "");

                // Подсчитываем общее количество строк в функции (включая пустые строки и комментарии)
                int lineCount = functionBody.Split(new[] { '\n' }).Length;

                // Подсчитываем количество непустых строк (без комментариев)
                string[] lines = functionBody.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                int nonEmptyLineCount = lines.Count(line => !string.IsNullOrWhiteSpace(line));

                // Подсчитываем количество ключевых слов в функции
                int keywordCount = Regex.Matches(functionBody, keywordPattern).Count;

                // Добавляем статистику о функции в объект FuncStat
                FuncStat.Name.Add(functionName);
                FuncStat.LineCount.Add(lineCount);
                FuncStat.NonEmptyLineCount.Add(nonEmptyLineCount);
                FuncStat.KeywordCount.Add(keywordCount);
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