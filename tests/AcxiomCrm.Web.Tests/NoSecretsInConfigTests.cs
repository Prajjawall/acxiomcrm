using System.Text.Json;
using System.Text.RegularExpressions;
using Shouldly;

namespace AcxiomCrm.Web.Tests;

public class NoSecretsInConfigTests
{
    [Fact]
    public void AppSettings_files_contain_no_secret_values()
    {
        var baseDir = AppContext.BaseDirectory;
        // Search up until we locate src/AcxiomCrm.Web
        var dir = new DirectoryInfo(baseDir);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "src", "AcxiomCrm.Web")))
        {
            dir = dir.Parent;
        }

        dir.ShouldNotBeNull("Could not find repository root directory");
        var webDir = Path.Combine(dir.FullName, "src", "AcxiomCrm.Web");
        var appsettingsFiles = Directory.GetFiles(webDir, "appsettings*.json", SearchOption.TopDirectoryOnly);
        appsettingsFiles.Length.ShouldBeGreaterThan(0, "No appsettings*.json files found in src/AcxiomCrm.Web");

        var secretKeyPattern = new Regex(@"(?i)password|secret|signingkey|connectionstring");

        foreach (var file in appsettingsFiles)
        {
            var content = File.ReadAllText(file);
            using var doc = JsonDocument.Parse(content);
            CheckElement(doc.RootElement, string.Empty, file, secretKeyPattern);
        }
    }

    private static void CheckElement(JsonElement element, string currentPath, string fileName, Regex secretKeyPattern)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    var propPath = string.IsNullOrEmpty(currentPath) ? prop.Name : $"{currentPath}:{prop.Name}";
                    if (secretKeyPattern.IsMatch(prop.Name))
                    {
                        if (prop.Value.ValueKind == JsonValueKind.String)
                        {
                            var val = prop.Value.GetString() ?? string.Empty;
                            if (!string.IsNullOrWhiteSpace(val))
                            {
                                // Allowed exception: ConnectionStrings:Default with (localdb) or __SET_VIA_ENV__
                                var isAllowedDefaultConn = propPath.Equals("ConnectionStrings:Default", StringComparison.OrdinalIgnoreCase)
                                    && (val.Contains("(localdb)", StringComparison.OrdinalIgnoreCase) || val.Equals("__SET_VIA_ENV__", StringComparison.OrdinalIgnoreCase));

                                if (!isAllowedDefaultConn)
                                {
                                    throw new ShouldAssertException($"Secret-like key '{propPath}' with non-empty value '{val}' found in '{Path.GetFileName(fileName)}'");
                                }
                            }
                        }
                    }

                    CheckElement(prop.Value, propPath, fileName, secretKeyPattern);
                }
                break;

            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    CheckElement(item, $"{currentPath}[{index}]", fileName, secretKeyPattern);
                    index++;
                }
                break;
        }
    }
}
