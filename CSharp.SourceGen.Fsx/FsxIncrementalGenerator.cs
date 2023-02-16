using System.Collections.Immutable;
using Microsoft.CodeAnalysis;


namespace CSharp.SourceGen.Fsx;


[Generator(LanguageNames.CSharp)]
public class FsxIncrementalGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var additionalFiles = context.AdditionalTextsProvider
            .Select(static (file, token) =>
            {
                var source = file.GetText(token)?.ToString() ?? string.Empty;
                return new AdditionalFile(file.Path, source);
            })
            .Where(static x => x.IsNotEmpty);

        var scripts = additionalFiles.Where(file =>
            file.FilePath.EndsWith(".fsx")
            && file.FileName[0] != '_'
            && file.IsNotEmpty);

        // Scripts can depend on other additional files. To call the generator on this files' changes
        // we need to include them into the cache
        var scriptsWithDependencies = scripts.Combine(additionalFiles.Collect())
            .Select(static (arg, _) =>
            {
                var (script, additionalFiles) = arg;
                var builder =
                    ImmutableArray.CreateBuilder<AdditionalFile>(additionalFiles.Length);
                foreach (var file in additionalFiles)
                {
                    if (script.Text.Contains(file.FileName))
                    {
                        builder.Add(file);
                    }
                }

                return (scriptToExecute: script, builder.ToImmutable());
            });

        context.RegisterSourceOutput(scriptsWithDependencies, (productionContext, script) =>
            CompileScript(productionContext, script.scriptToExecute.FilePath));
    }


    private static void CompileScript(SourceProductionContext context, string scriptFile)
    {
        var source =
            $"// Generated from '{Path.GetFileName(scriptFile)}'"
            + Environment.NewLine
            + ScriptRunner.Run(scriptFile, context.ReportDiagnostic, context.CancellationToken);
        context.AddSource(Path.GetFileNameWithoutExtension(scriptFile) + ".g", source);
    }


    private readonly record struct AdditionalFile(string FilePath, string Text)
    {
        public string FileName => Path.GetFileName(this.FilePath);
        public bool IsNotEmpty => !string.IsNullOrWhiteSpace(this.Text);
    }
}