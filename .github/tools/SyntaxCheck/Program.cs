using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

// 把指定資料夾底下每一個 .cs 用 Roslyn 剖析一次，有語法錯誤就回傳非 0。
//
// 這**不是**完整編譯：沒有遊戲組件就沒有型別資訊，「找不到 Radio」這類錯誤
// 這裡查不出來（那要靠 build job，見 ci.yml）。它查的是不需要參照就能確定的東西——
// 語法、括號、字串沒收尾、檔案編碼壞掉。
//
// 語言版本跟 mods/Common.props 的 LangVersion 一致，不然新語法會被誤判成錯誤。

string root = args.Length > 0 ? args[0] : "mods";
var parseOptions = new CSharpParseOptions(LanguageVersion.CSharp9);

if (!Directory.Exists(root))
{
    Console.WriteLine($"::error::找不到資料夾 {root}");
    return 2;
}

char sep = Path.DirectorySeparatorChar;
var files = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
    .Where(f => !f.Contains($"{sep}obj{sep}") && !f.Contains($"{sep}bin{sep}"))
    .OrderBy(f => f, StringComparer.Ordinal)
    .ToList();

int errors = 0;
foreach (string file in files)
{
    var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file), parseOptions, path: file);
    foreach (var d in tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error))
    {
        errors++;
        var line = d.Location.GetLineSpan().StartLinePosition;
        // ::error:: 這個格式 GitHub 會直接標在 PR 的那一行上
        Console.WriteLine($"::error file={file},line={line.Line + 1},col={line.Character + 1}::{d.Id} {d.GetMessage()}");
    }
}

Console.WriteLine($"剖析了 {files.Count} 個檔案，{errors} 個語法錯誤。");
return errors == 0 ? 0 : 1;
