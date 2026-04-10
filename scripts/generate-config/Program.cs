using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using System.Text.Json.Nodes;
using Azure.Identity;
using Azure.Core;

class Program
{
    static async Task<int> Main(string[] args)
    {
        try
        {
            var connection = GetArgValue(args, "--connection") ?? Environment.GetEnvironmentVariable("SEED_SOURCE_CONNECTION");
            var outDir = GetArgValue(args, "--out-dir") ?? Path.Combine("src", "backend", "DocumentIA.Functions", "config");
            var dryRun = Array.Exists(args, a => string.Equals(a, "--dry-run", StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrWhiteSpace(connection))
            {
                Console.Error.WriteLine("Connection string must be provided via --connection or SEED_SOURCE_CONNECTION env var.");
                return 2;
            }

            Console.WriteLine($"Out dir: {outDir}  (dry-run: {dryRun})");

            if (!dryRun && Directory.Exists(outDir))
            {
                var backupRoot = Path.Combine("scripts", "seeds", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
                Directory.CreateDirectory(backupRoot);
                var dest = Path.Combine(backupRoot, "config");
                Directory.Move(outDir, dest);
                Console.WriteLine($"Existing config moved to {dest}");
            }

            // Ensure target folders exist (if not dry-run)
            if (!dryRun)
            {
                Directory.CreateDirectory(outDir);
                Directory.CreateDirectory(Path.Combine(outDir, "tipologias"));
                Directory.CreateDirectory(Path.Combine(outDir, "classification"));
                Directory.CreateDirectory(Path.Combine(outDir, "extraction"));
                Directory.CreateDirectory(Path.Combine(outDir, "prompt"));
                Directory.CreateDirectory(Path.Combine(outDir, "layout"));
            }

            using var conn = new SqlConnection(connection);

            // If connection string indicates Managed Identity usage, obtain token
            if (connection.IndexOf("Authentication", StringComparison.OrdinalIgnoreCase) >= 0 &&
                connection.IndexOf("Managed", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Console.WriteLine("Using Azure Default Credentials to acquire access token for SQL.");
                var credential = new DefaultAzureCredential();
                var tokenRequestContext = new TokenRequestContext(new[] { "https://database.windows.net/.default" });
                var token = await credential.GetTokenAsync(tokenRequestContext, default);
                conn.AccessToken = token.Token;
            }

            await conn.OpenAsync();
            Console.WriteLine("Connected to database.");

            // Load tipologias
            var tipologias = new List<(string Codigo, string Nombre, string Version, string Config)>();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT Codigo, Nombre, COALESCE(VersionPublicada, Version) AS Version, ConfiguracionJson FROM Tipologias;";
                using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                {
                    var codigo = rdr.IsDBNull(0) ? string.Empty : rdr.GetString(0);
                    var nombre = rdr.IsDBNull(1) ? string.Empty : rdr.GetString(1);
                    var version = rdr.IsDBNull(2) ? string.Empty : rdr.GetString(2);
                    var conf = rdr.IsDBNull(3) ? string.Empty : rdr.GetString(3);
                    tipologias.Add((codigo, nombre, version, conf));
                }
            }

            foreach (var t in tipologias)
            {
                var path = Path.Combine(outDir, "tipologias", t.Codigo + ".validation.json");
                JsonNode? node = null;
                if (!string.IsNullOrWhiteSpace(t.Config))
                {
                    try
                    {
                        node = JsonNode.Parse(t.Config);
                        if (node is JsonObject obj)
                        {
                            if (!obj.ContainsKey("TipologiaId")) obj["TipologiaId"] = t.Codigo;
                            if (!obj.ContainsKey("TipologiaNombre")) obj["TipologiaNombre"] = t.Nombre;
                            if (!obj.ContainsKey("Version")) obj["Version"] = t.Version;
                            var formatted = obj.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
                            if (!dryRun) await File.WriteAllTextAsync(path, formatted);
                            Console.WriteLine($"Prepared tipologia {t.Codigo} -> {path}");
                            continue;
                        }
                    }
                    catch (Exception ex)
                    {
                        var wrap = new JsonObject
                        {
                            ["TipologiaId"] = t.Codigo,
                            ["TipologiaNombre"] = t.Nombre,
                            ["Version"] = t.Version,
                            ["RawConfiguracionJson"] = t.Config,
                            ["_parseError"] = ex.Message
                        };
                        var formatted = wrap.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
                        if (!dryRun) await File.WriteAllTextAsync(path, formatted);
                        Console.WriteLine($"Prepared tipologia {t.Codigo} (parse error) -> {path}");
                        continue;
                    }
                }

                // If no config, create minimal shape
                var minimal = new JsonObject
                {
                    ["TipologiaId"] = t.Codigo,
                    ["TipologiaNombre"] = t.Nombre,
                    ["Version"] = t.Version,
                    ["Fields"] = new JsonArray()
                };
                var minFormatted = minimal.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
                if (!dryRun) await File.WriteAllTextAsync(path, minFormatted);
                Console.WriteLine($"Prepared tipologia {t.Codigo} (generated) -> {path}");
            }

            // Load plugins
            var plugins = new List<(string Codigo, string Config)>();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT TipologiaCodigo, ConfiguracionJson FROM PluginTipologiaConfigs;";
                using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                {
                    var codigo = rdr.IsDBNull(0) ? string.Empty : rdr.GetString(0);
                    var conf = rdr.IsDBNull(1) ? string.Empty : rdr.GetString(1);
                    plugins.Add((codigo, conf));
                }
            }

            foreach (var p in plugins)
            {
                if (string.IsNullOrWhiteSpace(p.Config))
                {
                    Console.WriteLine($"Skipped plugin for {p.Codigo} (empty)");
                    continue;
                }
                var path = Path.Combine(outDir, "tipologias", p.Codigo + ".plugins.json");
                try
                {
                    var node = JsonNode.Parse(p.Config);
                    var formatted = node.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
                    if (!dryRun) await File.WriteAllTextAsync(path, formatted);
                    Console.WriteLine($"Prepared plugin for {p.Codigo} -> {path}");
                }
                catch
                {
                    if (!dryRun) await File.WriteAllTextAsync(path, p.Config);
                    Console.WriteLine($"Prepared plugin raw for {p.Codigo} -> {path}");
                }
            }

            // Load models
            var modelsByType = new Dictionary<int, List<JsonNode?>>();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT Tipo, [Key], Provider, ConfiguracionJson FROM ModeloConfigs WHERE Activo = 1;";
                using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                {
                    var tipo = rdr.IsDBNull(0) ? 0 : rdr.GetInt32(0);
                    var key = rdr.IsDBNull(1) ? string.Empty : rdr.GetString(1);
                    var provider = rdr.IsDBNull(2) ? string.Empty : rdr.GetString(2);
                    var conf = rdr.IsDBNull(3) ? string.Empty : rdr.GetString(3);
                    JsonNode node = new JsonObject();
                    if (!string.IsNullOrWhiteSpace(conf))
                    {
                        try
                        {
                            var parsed = JsonNode.Parse(conf) as JsonObject ?? new JsonObject();
                            node = parsed;
                        }
                        catch
                        {
                            node = new JsonObject { ["RawConfiguracionJson"] = conf };
                        }
                    }
                    if (node is JsonObject obj)
                    {
                        obj["Key"] = key;
                        obj["Provider"] = provider;
                    }
                    if (!modelsByType.ContainsKey(tipo)) modelsByType[tipo] = new List<JsonNode?>();
                    modelsByType[tipo].Add(node);
                }
            }

            void WriteModels(int tipo, string subfolder)
            {
                var list = modelsByType.ContainsKey(tipo) ? modelsByType[tipo] : new List<JsonNode?>();
                var arr = new JsonArray();
                foreach (var n in list) arr.Add(n ?? new JsonObject());
                var root = new JsonObject { ["Models"] = arr };
                var path = Path.Combine(outDir, subfolder, "models.json");
                var formatted = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
                if (!dryRun) File.WriteAllText(path, formatted);
                Console.WriteLine($"Prepared models for {subfolder} -> {path} ({arr.Count} models)");
            }

            WriteModels(0, "classification");
            WriteModels(1, "extraction");
            WriteModels(2, "prompt");
            WriteModels(3, "layout");

            await conn.CloseAsync();
            Console.WriteLine("Extraction completed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal error: {ex.Message}");
            return 3;
        }
    }

    static string? GetArgValue(string[] args, string name)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                return args[i + 1];
            }
        }
        return null;
    }
}
