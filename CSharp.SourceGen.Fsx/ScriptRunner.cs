﻿using System.Diagnostics;
using System.Text;
using Microsoft.CodeAnalysis;


namespace CSharp.SourceGen.Fsx;


public static class ScriptRunner
{
    public static string Run(string filePath, Action<Diagnostic> reportDiagnostic,
        CancellationToken token)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                // Full path is a fix for JetBrains Rider, because it ships with it's own dotnet.exe, that does not have fsi
                FileName = @"C:\Program Files\dotnet\dotnet.exe",
                Arguments = $"fsi --utf8output --exec {Path.GetFileName(filePath)}",
                WorkingDirectory = Path.GetDirectoryName(filePath),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                StandardOutputEncoding = Encoding.UTF8,
                RedirectStandardError = true
            }
        };

        process.Start();

        var sourceBuilder = BeginReadOutput(process);
        var errors = BeginReadErrors(process);

        while (!process.WaitForExit(50))
        {
            if (token.IsCancellationRequested)
            {
                process.Kill();
                process.WaitForExit(5000);
                token.ThrowIfCancellationRequested();
            }
        }

        if (process.ExitCode == 0)
        {
            foreach (var error in errors)
            {
                reportDiagnostic(Diagnostic.Create(ScriptWarning, Location.None, error));
            }
        }
        else
        {
            foreach (var error in errors)
            {
                reportDiagnostic(Diagnostic.Create(ScriptError, Location.None, error));
            }
        }

        return sourceBuilder.ToString();


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


    private static readonly DiagnosticDescriptor ScriptError = new(
        id: $"{DiagnosticIdPrefix}01",
        title: "C# script threw exception",
        messageFormat: "{0}",
        category: DiagnosticCategory,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);


    private static readonly DiagnosticDescriptor ScriptWarning = new(
        id: $"{DiagnosticIdPrefix}02",
        title: "C# script produced warning",
        messageFormat: "{0}",
        category: DiagnosticCategory,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);


    private const string DiagnosticIdPrefix = "SourceGen.Fsx";
    private const string DiagnosticCategory = "CSharp.SourceGen.Fsx";
}