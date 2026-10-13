namespace CreateFile.Core;
public static class BuiltIns
{
    public static List<FilePreset> Presets() =>
    [
        new("Python", ".py", "main.py", true), new("JavaScript", ".js", "index.js", true),
        new("TypeScript", ".ts", "index.ts"), new("TSX", ".tsx", "App.tsx", true), new("JSX", ".jsx", "App.jsx"),
        new("HTML", ".html", "index.html", true), new("CSS", ".css", "style.css"), new("SCSS", ".scss", "style.scss"),
        new("JSON", ".json", "package.json", true), new("Markdown", ".md", "README.md", true),
        new("YAML", ".yaml", "config.yaml"), new("XML", ".xml", "document.xml"),
        new("C", ".c", "main.c"), new("C++", ".cpp", "main.cpp"), new("C#", ".cs", "Program.cs"),
        new("Java", ".java", "Main.java"), new("Rust", ".rs", "main.rs"), new("Go", ".go", "main.go"),
        new("PowerShell", ".ps1", "script.ps1"), new("SQL", ".sql", "query.sql"), new("Bash", ".sh", "script.sh"),
        new("Plain text", ".txt", "New file.txt"), new("Environment", "", ".env"),
        new("Git ignore", "", ".gitignore"), new("Dockerfile", "", "Dockerfile")
    ];
    public static List<TextTemplate> Templates() =>
    [
        new("html", "HTML5 starter", "<!doctype html>\n<html lang=\"en\">\n<head>\n  <meta charset=\"utf-8\">\n  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n  <title>Document</title>\n</head>\n<body>\n  <h1>Hello, world!</h1>\n</body>\n</html>\n"),
        new("python", "Python main", "def main():\n    print(\"Hello, world!\")\n\n\nif __name__ == \"__main__\":\n    main()\n"),
        new("js", "JavaScript starter", "\"use strict\";\n\nconsole.log(\"Hello, world!\");\n"),
        new("tsx", "React TSX component", "export default function App() {\n  return <h1>Hello, world!</h1>;\n}\n"),
        new("json", "JSON object", "{}\n"), new("readme", "Markdown README", "# Project\n\nDescribe your project here.\n"),
        new("cpp", "C++ main", "#include <iostream>\n\nint main() {\n    std::cout << \"Hello, world!\\n\";\n}\n"),
        new("cs", "C# program", "Console.WriteLine(\"Hello, world!\");\n")
    ];
}
