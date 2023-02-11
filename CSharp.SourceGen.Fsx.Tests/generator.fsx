#r "nuget: Scriban, 5.4.6"

open System.IO

"
public static class Generated
{
    public const string Content = \"\"\"
    {{ content }}
    \"\"\";
}
"
|> fun template -> Scriban.Template
                       .Parse(template)
                       .Render({| content = File.ReadAllText "content.txt" |})
|> printfn "%s"