using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Diagnostics;
using System.Text;
using System.IO.Compression;
using System.Data;
using System.Dynamic;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Security;
using System.Xml;

namespace Cpyt
{
    // 🔥 CONTEXT MANAGER (fora da classe para compilar) 🔥
    public class ContextManager : IDisposable
    {
        public Action OnEnter, OnExit;
        public void Dispose() => OnExit?.Invoke();
        public void __enter__() => OnEnter?.Invoke();
        public void __exit__() => OnExit?.Invoke();
    }

    public static class cs
    {
        private static string _virtualCwd = "/storage/emulated/0";
        private static readonly string _signalDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "csign_signals");

        // ✅ UNIFICADO: Apenas PyDict (melhor que ExpandoObject)
        public static readonly dynamic py = new PyDict();
        public static dynamic Py => py;

        // ========== INPUT/OUTPUT PYTHONIZADO ==========
        public static T print<T>(T valor) { Console.WriteLine(valor?.ToString() ?? "null"); return valor; }
        public static T printIl<T>(T valor) { Console.Write(valor?.ToString() ?? "null"); return valor; }
        public static void PyPrint(string texto) => Console.WriteLine(texto);  // 100% Python print()
        public static void PyPrintIl(string texto) => Console.Write(texto);     // 100% Python print()

        public static T input<T>(string prompt = "")
        {
            Console.Write(prompt);
            string entrada = Console.ReadLine() ?? "";
            try
            {
                if (typeof(T) == typeof(bool)) return (T)(object)bool.Parse(entrada.ToLower());
                return (T)Convert.ChangeType(entrada, typeof(T));
            }
            catch { return default; }
        }

        // ========== FILE I/O ==========
        public static void write(string path, object conteudo) =>
            File.WriteAllText(Path.Combine(_virtualCwd, path), conteudo?.ToString());

        public static string read(string path) =>
            File.Exists(Path.Combine(_virtualCwd, path)) ? File.ReadAllText(Path.Combine(_virtualCwd, path)) : "";

        // ========== VARIÁVEIS GLOBAIS PYTHON-STYLE ==========
        public static dynamic var(string nome, object valor = null)
        {
            var dict = (IDictionary<string, object>)py;
            if (valor == null)
                return dict.ContainsKey(nome) ? dict[nome] : "";

            dynamic oldValue = dict.ContainsKey(nome) ? dict[nome] : null;
            dict[nome] = valor;

            if (Watchers.ContainsKey(nome) && !Equals(oldValue, valor))
                foreach (var watcher in Watchers[nome])
                    watcher(valor);

            return valor;
        }

        public static readonly Dictionary<string, List<Action<object>>> Watchers =
            new Dictionary<string, List<Action<object>>>();

        public static void watch(string nome, Action<object> acao)
        {
            if (!Watchers.ContainsKey(nome))
                Watchers[nome] = new List<Action<object>>();
            Watchers[nome].Add(acao);
        }

        public static void unwatch(string nome) => Watchers.Remove(nome);
        public static void clear_vars() => ((IDictionary<string, object>)py).Clear();

        // ========== FUNÇÕES E LÂMBDAS ==========
        public static dynamic def(string nome, Func<object[], object> func)
        {
            ((IDictionary<string, object>)py)[nome] = func;
            return func;
        }

        public static object call(string nome, params object[] args)
        {
            var dict = (IDictionary<string, object>)py;
            return dict.TryGetValue(nome, out var func) && func is Func<object[], object> f ? f(args) : null;
        }

        public static dynamic lambda(string nome, string expr)
        {
            return def(nome, args =>
            {
                var x = args.Length > 0 ? args[0] : 0;
                return new DataTable().Compute(expr.Replace("x", x.ToString()), null);
            });
        }

        // ========== LIST COMPREHENSIONS E ITERADORES ==========
        public static List<T> lc<T>(string expressao, string colecaoNome)
        {
            var dict = (IDictionary<string, object>)py;
            var lista = (dict.ContainsKey(colecaoNome) ? dict[colecaoNome] : null) as List<object> ??
                        colecaoNome.Split(',').Select(x => (object)x.Trim()).ToList();

            var resultado = new List<T>();
            foreach (var item in lista)
            {
                try
                {
                    string eval = expressao.Replace("x", item.ToString());
                    var val = new DataTable().Compute(eval, null);
                    resultado.Add((T)Convert.ChangeType(val, typeof(T)));
                }
                catch { resultado.Add(default); }
            }
            return resultado;
        }

        public static List<T> map<T>(Func<object, T> func, List<object> lista) =>
            lista.Select(func).Cast<T>().ToList();

        public static List<T> filter<T>(Func<object, bool> func, List<object> lista) =>
            lista.Where(func).Cast<T>().ToList();

        public static T reduce<T>(Func<T, T, T> func, List<T> lista) =>
            lista.Aggregate(func);

        public static List<int> rangePy(int start, int end, int step = 1)
        {
            if (step == 0) return new List<int>();
            var list = new List<int>();
            if (step > 0) for (int i = start; i < end; i += step) list.Add(i);
            else for (int i = start; i > end; i += step) list.Add(i);
            return list;
        }

        public static int len(object obj) => obj switch
        {
            string s => s.Length,
            System.Collections.ICollection c => c.Count,
            _ => 0
        };

        // ========== PYTHON SUPERPOWERS (v3.0) ==========
        public static ContextManager @with(Action enter, Action exit) =>
            new() { OnEnter = enter, OnExit = exit };

        public static async Task @async(Func<Task> func)
        {
            try { await func(); }
            catch (Exception ex) { print($"ERROR: {ex.Message}"); }
        }
        public static T match<T>(object value, Dictionary<string, Func<T>> cases, Func<T>? def = null)
            where T : class  // Só classes (pode ser null)
        {
            string typeName = value?.GetType().Name.ToLower() ?? "null";
            return cases.TryGetValue(typeName, out var handler) ? handler() : def?.Invoke() ?? default(T);
        }






        public static dynamic @let(string name, Func<object> expr)
        {
            var value = expr();
            var(name, value);
            return value;
        }

        public static dynamic @class(Dictionary<string, object> methods)
        {
            var obj = new ExpandoObject();
            var dict = (IDictionary<string, object>)obj;
            foreach (var kv in methods) dict[kv.Key] = kv.Value;
            return obj;
        }

        public static void @import(string moduleName, Action moduleCode)
        {
            moduleCode();
            var(moduleName, py);
        }

        public static void assert(bool condicao, string mensagem = "Assertion failed!")
        {
            if (!condicao) throw new Exception(mensagem);
        }

        public static Func<T, R> timeit<T, R>(Func<T, R> func)
        {
            return arg =>
            {
                var sw = Stopwatch.StartNew();
                var result = func(arg);
                print($"⏱ {func.Method.Name}: {sw.ElapsedMilliseconds}ms");
                return result;
            };
        }

        public static T pipe<T>(this T value, Func<T, T> func) => func(value);

        public static void @for(string varName, List<object> colecao, Action action)
        {
            for (int i = 0; i < colecao.Count; i++)
            {
                var(varName, colecao[i]);
                var("i", i);
                action();
            }
        }

        public static IEnumerable<T> generator<T>(Func<int, T> func, int count)
        {
            for (int i = 0; i < count; i++) yield return func(i);
        }

        public static List<T> list<T>(IEnumerable<T> gen) => gen.ToList();

        // ========== ASYNC ==========
        public static async Task sleep(int ms) => await Task.Delay(ms);
        public static async Task<T> timeout<T>(Func<T> func, int ms)
        {
            var cts = new CancellationTokenSource(ms);
            try { return await Task.Run(func, cts.Token); }
            catch { return default; }
        }

        // ========== UTILITÁRIOS ==========
        public static dynamic hint(string tipo, object valor) => tipo.ToLower() switch
        {
            "int" or "i" => Convert.ToInt32(valor),
            "float" or "f" => Convert.ToDouble(valor),
            "str" or "s" => valor?.ToString(),
            "bool" or "b" => bool.Parse(valor.ToString().ToLower()),
            _ => valor
        };

        public static string type(object obj) => obj?.GetType().Name ?? "None";
        public static bool isinstance(object obj, string tipo) =>
            tipo switch { "int" => obj is int, "str" => obj is string, _ => false };

        public static string f(string fmt, params object[] args) =>
            string.Format(fmt.Replace("{{", "{").Replace("}}", "}"), args);

        public static string f2(string template, object obj)
        {
            var dict = (IDictionary<string, object>)py;
            string result = template;
            foreach (var kv in dict)
                result = result.Replace($"{{{kv.Key}}}", kv.Value?.ToString() ?? "");
            return result;
        }

        public static dynamic dict(params object[] pares)
        {
            var d = new ExpandoObject();
            var dict = (IDictionary<string, object>)d;
            for (int i = 0; i < pares.Length; i += 2)
                dict[pares[i]?.ToString()] = pares[i + 1];
            return d;
        }

        // ========== PERSISTÊNCIA ==========
        public static void save_json(string arquivo = "data.json")
        {
            var dict = (IDictionary<string, object>)py;
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(dict, options);
            File.WriteAllText(Path.Combine(_virtualCwd, arquivo), json);
        }

        public static void load_json(string arquivo = "data.json")
        {
            string path = Path.Combine(_virtualCwd, arquivo);
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                if (dict != null)
                    foreach (var kv in dict) var(kv.Key, kv.Value);
            }
            else print("Arquivo não existe.");
        }

        public static void save_xml(string arquivo = "data.xml")
        {
            var dict = (IDictionary<string, object>)py;
            using var writer = new StringWriter();
            writer.WriteLine("<csign>");
            foreach (var kv in dict)
            {
                string valor = kv.Value?.ToString() ?? "";
                writer.WriteLine($"  <{kv.Key}>{SecurityElement.Escape(valor)}</{kv.Key}>");
            }
            writer.WriteLine("</csign>");
            File.WriteAllText(Path.Combine(_virtualCwd, arquivo), writer.ToString(), Encoding.UTF8);
        }

        public static void load_xml(string arquivo = "data.xml")
        {
            string path = Path.Combine(_virtualCwd, arquivo);
            if (File.Exists(path))
            {
                var xmlDoc = new XmlDocument();
                xmlDoc.Load(path);
                foreach (XmlNode node in xmlDoc.DocumentElement.ChildNodes)
                    var(node.Name, node.InnerText);
            }
            else print("Arquivo não existe.");
        }

        public static async Task<string> fetch(string url)
        {
            try
            {
                using var client = new System.Net.Http.HttpClient();
                return await client.GetStringAsync(url);
            }
            catch { return "❌ Erro HTTP"; }
        }

        // ========== UI/UX ==========
        public static void clear() => Console.Clear();
        public static void Color(ConsoleColor c) => Console.ForegroundColor = c;
        public static void ResetColor() => Console.ResetColor();
        public static void Wait(int ms) => Thread.Sleep(ms);
        public static void Beep(int f, int d) { try { Console.Beep(f, d); } catch { } }

        public static void ProgressBar(int atual, int total, int largura = 30)
        {
            double pct = Math.Clamp((double)atual / total, 0, 1);
            int barras = (int)(largura * pct);
            string barra = new string('█', barras) + new string('░', largura - barras);
            Console.Write($"[{barra}] {(pct * 100):F0}% ");
            if (atual >= total) Console.WriteLine();
        }

        // ========== SIGNALING & P2P ==========
        public static void SaveConfig(string k, string v)
        {
            try
            {
                Directory.CreateDirectory(_signalDir);
                File.AppendAllLines(Path.Combine(_signalDir, "config.ini"), new[] { $"{k}={v}" });
            }
            catch { }
        }

        public static string LoadConfig(string k)
        {
            try
            {
                string p = Path.Combine(_signalDir, "config.ini");
                return File.ReadAllLines(p).LastOrDefault(l => l.StartsWith(k + "="))?.Split('=')[1];
            }
            catch { return null; }
        }

        public static string GetUserId(string app = "CSIGN")
        {
            string id = LoadConfig($"id_{app}");
            if (string.IsNullOrEmpty(id))
            {
                id = Guid.NewGuid().ToString()[..8];
                SaveConfig($"id_{app}", id);
            }
            return id;
        }

        // ========== SUPER SHELL v3.0 UNIFICADO ==========
        public static void CSH()
        {
            Color(ConsoleColor.Cyan);
            clear();
            string meuId = GetUserId("CSH");
            print($@"
╔══════════════════════════════════════╗
║     🔥 CSH v3.0 PYTHONIZADO 🔥      ║
║          ID: {meuId}                ║
║  map filter @class pipe timeit lc   ║
╚══════════════════════════════════════╝");

            while (true)
            {
                string linha = input<string>($"🐍 {_virtualCwd} $> ");
                if (new[] { "exit", "sair", "quit" }.Contains(linha.Trim().ToLower())) break;
                Interpretar(linha);
            }
            ResetColor();
        }

        public static void Interpretar(string linha)
        {
            if (string.IsNullOrWhiteSpace(linha)) return;
            string[] partes = linha.Trim().Split(' ', 2);
            string cmd = partes[0].ToUpper();
            string arg = partes.Length > 1 ? partes[1] : "";

            SwitchAction(cmd, new Dictionary<string, Action> {
                { "HELP", () => print("ls var map class pipe demo progress calc lc fetch") },
                { "VAR", () => {
                    var d = (IDictionary<string, object>)py;
                    if (string.IsNullOrEmpty(arg)) {
                        foreach (var v in d) print($"{v.Key} = {v.Value}");
                    } else if (arg.Contains("=")) {
                        var p = arg.Split('=');
                        var(p[0].Trim(), p[1].Trim());
                        print($"{p[0].Trim()} = OK");
                    }
                }},
               // { "MAP", () => print(map(x => (int)x * 2, py.numeros ?? new List<object>{1,2,3,4,5})) },
                { "LC", () => print(lc<int>("x*2", "1,2,3,4,5")) },
                { "CLASS", () => {
                    var pessoa = @class(new Dictionary<string, object>{ {"nome", "João"}, {"idade", 25} });
                    var("pessoa", pessoa);
                    print(pessoa.nome);
                }},
                { "PIPE", () => print(42.pipe(x => x*2).pipe(x => x+10)) },
                { "DEMO", DemoPySharp },
                { "PROGRESS", () => {
                    print("⏳ Progress:");
                    for(int i=0; i<=100; i+=10) { ProgressBar(i, 100); Wait(100); }
                }},
                { "CLS", () => clear() },
                { "LS", ListFiles },
                { "CALC", () => { try { print(new DataTable().Compute(arg, null)); } catch { print("Erro"); } } },
                { "FETCH", () => { /* async demo */ print("fetch https://api.github.com"); } }
            }, () => print($"❌ '{cmd}' inválido. HELP"));
        }

        public static void ListFiles()
        {
            try
            {
                print("📁 Diretórios:");
                foreach (var d in Directory.GetDirectories(_virtualCwd))
                    print($"  {Path.GetFileName(d)}/");
                print("📄 Arquivos:");
                foreach (var f in Directory.GetFiles(_virtualCwd))
                    print($"  {Path.GetFileName(f)}");
            }
            catch { print("❌ Erro de acesso."); }
        }

        public static void SwitchAction(string k, Dictionary<string, Action> casos, Action padrao = null)
        {
            if (casos.TryGetValue(k, out Action a)) a(); else padrao?.Invoke();
        }

        public static void DemoPySharp()
        {
            print("🚀 CSH v3.0 DEMO PYTHONIZADO 🔥");
            var("numeros", new List<object> { 1, 2, 3, 4, 5 });
            // print("Map x2: " + map(x => (int)x * 2, py.numeros));
            print("LC x2: " + lc<int>("x*2", "1,2,3,4,5"));
            //print("Filter pares: " + filter(x => (int)x % 2 == 0, py.numeros));
            print("Pipe: " + 42.pipe(x => x * 2).pipe(x => x + 10));
            //print(timeit(x => (int)x * 100)(7));
        }

        // Decorators mantidos
        public static class Decorators
        {
            public static Func<T, R> cache<T, R>(Func<T, R> func)
            {
                var cache = new Dictionary<T, R>();
                return arg => cache.TryGetValue(arg, out var value) ? value : cache[arg] = func(arg);
            }

            public static Func<T, R> retry<T, R>(Func<T, R> func, int tentativas = 3)
            {
                return arg =>
                {
                    while (tentativas-- > 0)
                    {
                        try { return func(arg); }
                        catch (Exception ex)
                        {
                            print(ex.Message);
                            if (tentativas == 0) throw;
                            Wait(100);
                        }
                    }
                    throw new Exception("all attempts failed");
                };
            }
        }
    }

    // 🔥 PYDICT UNIFICADO (melhor que ExpandoObject) 🔥
    public class PyDict : DynamicObject
    {
        private readonly Dictionary<string, object> _dict = new();

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            result = _dict.TryGetValue(binder.Name, out var value) ? value : null;
            return true;
        }

        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            _dict[binder.Name] = value;
            if (cs.Watchers.ContainsKey(binder.Name))
                foreach (var watcher in cs.Watchers[binder.Name])
                    watcher(value);
            return true;
        }
    }
}