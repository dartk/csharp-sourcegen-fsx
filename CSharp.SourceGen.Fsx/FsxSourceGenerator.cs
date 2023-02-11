using System.Diagnostics;
using System.Text;
using Microsoft.CodeAnalysis;


namespace CSharp.SourceGen.Fsx;


[Generator]
public class CsxGenerator : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context)
    {
    }


    public void Execute(GeneratorExecutionContext context)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet.exe",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            StandardOutputEncoding = Encoding.UTF8,
            RedirectStandardError = true
        };

        foreach (var file in context.AdditionalFiles)
        {
            if (Path.GetExtension(file.Path) != ".fsx")
            {
                continue;
            }

            var filePath = Path.GetFullPath(file.Path);

            startInfo.Arguments = $"fsi --utf8output --exec {Path.GetFileName(filePath)}";
            startInfo.WorkingDirectory = Path.GetDirectoryName(filePath);
            var process = new Process { StartInfo = startInfo };
            process.Start();

            var sourceBuilder = BeginReadOutput(process);
            var errors = BeginReadErrors(process);

            while (!process.WaitForExit(50))
            {
                if (context.CancellationToken.IsCancellationRequested)
                {
                    process.Kill();
                    process.WaitForExit(5000);
                    return;
                }
            }

            context.AddSource(Path.GetFileName(filePath) + ".out", sourceBuilder.ToString());

            if (process.ExitCode == 0)
            {
                foreach (var error in errors)
                {
                    context.ReportDiagnostic(Diagnostic.Create(ScriptWarning,
                        Location.None, error));
                }
            }
            else
            {
                foreach (var error in errors)
                {
                    context.ReportDiagnostic(Diagnostic.Create(ScriptError,
                        Location.None, error));
                }
            }


            static StringBuilder BeginReadOutput(Process process)
            {
                var output = new StringBuilder();

                process.OutputDataReceived += (_, args) => output.AppendLine(args.Data);
                process.BeginOutputReadLine();

                return output;
            }


            static List<string> BeginReadErrors(Process process)
            {
                var errors = new List<string>();

                process.ErrorDataReceived += (_, args) =>
                {
                    var line = args.Data;
                    if (!string.IsNullOrEmpty(line))
                    {
                        errors.Add(line);
                    }
                };
                process.BeginErrorReadLine();

                return errors;
            }
        }
    }


    private static readonly DiagnosticDescriptor ScriptError = new(
        id: $"{DiagnosticIdPrefix}001",
        title: "F# script threw exception",
        messageFormat: "{0}",
        category: DiagnosticCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);


    private static readonly DiagnosticDescriptor ScriptWarning = new(
        id: $"{DiagnosticIdPrefix}002",
        title: "F# script produced warning",
        messageFormat: "{0}",
        category: DiagnosticCategory,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);


    private static readonly DiagnosticDescriptor ScriptExecutionTimeout = new(
        id: $"{DiagnosticIdPrefix}003",
        title: "F# script execution timed out",
        messageFormat: "'{0}' execution timed out",
        category: DiagnosticCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);


    private const string DiagnosticIdPrefix = "SourceGen.Fsx";
    private const string DiagnosticCategory = "CSharp.SourceGen.Fsx";
}