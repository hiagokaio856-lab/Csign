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


namespace Csign
{

	public static class cs
	{
		private static string _virtualCwd = "/storage/emulated/0";
		private static readonly string _signalDir = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "csign_signals");


		public static readonly dynamic py = new ExpandoObject();

		public static T print<T>(T valor) { Console.WriteLine(valor?.ToString() ?? "null"); return valor; }
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


		public static void write(string path, object conteudo) =>
			File.WriteAllText(Path.Combine(_virtualCwd, path), conteudo?.ToString());

		public static string read(string path) =>
			File.Exists(Path.Combine(_virtualCwd, path)) ? File.ReadAllText(Path.Combine(_virtualCwd, path)) : "";

		public static dynamic get(string nome)
		{
			var dict = (IDictionary<string, object>)py;
			return dict.TryGetValue(nome, out var v) ? v : null;
		}


		public static async Task sleep(int ms) => await Task.Delay(ms);
		public static async Task<T> timeout<T>(Func<T> func, int ms)
		{
			var cts = new CancellationTokenSource(ms);
			try { return await Task.Run(func, cts.Token); }
			catch { print("⏰ Timeout!"); return default; }
		}

		public static void clear_vars() => ((IDictionary<string, object>)py).Clear();

		public static void save_session(string arquivo = "session.json")
		{
			var dict = (IDictionary<string, object>)py;

			string json = "{ " + string.Join(", ", dict.Select(kv => $"\"{kv.Key}\": \"{kv.Value}\"")) + " }";
			File.WriteAllText(Path.Combine(_signalDir, arquivo), json);
		}


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

		public static class Decorators
		{

			public static Func<T, R> cache<T, R>(Func<T, R> func)
			{
				var cache = new Dictionary<T, R>();
				return arg => cache.TryGetValue(arg, out var value) ? value : cache[arg] = func(arg);
			}

			public static Func<T, R> timer<T, R>(Func<T, R> func)
			{
				return arg =>
				{
					var sw = Stopwatch.StartNew();
					var result = func(arg);
					print($"⏱️ {func.Method.Name}: {sw.ElapsedMilliseconds}ms");
					return result;
				};
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
							print($"⚠️ Tentativa falhou: {ex.Message}");
							if (tentativas == 0) throw;
							Wait(100);
						}
					}
					throw new Exception("Todas tentativas falharam");
				};
			}
		}

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

		public static dynamic hint(string tipo, object valor) => tipo.ToLower() switch
		{
			"int" or "i" => Convert.ToInt32(valor),
			"float" or "f" => Convert.ToDouble(valor),
			"str" or "s" => valor?.ToString(),
			"bool" or "b" => bool.Parse(valor.ToString().ToLower()),
			_ => valor
		};
		public static void SwitchAction(string k, Dictionary<string, Action> casos, Action padrao = null)
		{
			if (casos.TryGetValue(k, out Action a)) a(); else padrao?.Invoke();
		}

		public static T SwitchValue<T>(string k, Dictionary<string, T> casos, T padrao) =>
			casos.TryGetValue(k, out T r) ? r : padrao;

		public static void For(int s, int e, Action<int> a) { for (int i = s; i < e; i++) a(i); }
		public static void clear() => Console.Clear();
		public static void Color(ConsoleColor c) => Console.ForegroundColor = c;
		public static void ResetColor() => Console.ResetColor();
		public static void Wait(int ms) => Thread.Sleep(ms);
		public static void Beep(int f, int d) { try { Console.Beep(f, d); } catch { } }

		public static decimal Menu(string titulo, params (string texto, decimal valor)[] opcoes)
		{
			clear();
			print($"=== {titulo} ===");
			foreach (var op in opcoes) print($"{op.valor}. {op.texto}");
			return input<decimal>("➤ ");
		}

		public static void ProgressBar(int atual, int total, int largura = 30)
		{
			double pct = Math.Clamp((double)atual / total, 0, 1);
			int barras = (int)(largura * pct);
			string barra = new string('█', barras) + new string('░', largura - barras);
			Console.Write($"\r[{barra}] {(pct * 100):F0}%");
			if (atual >= total) Console.WriteLine();
		}


		private static readonly Dictionary<string, List<Action<object>>> Watchers = new Dictionary<string, List<Action<object>>>();

		public static void watch(string nome, Action<object> acao)
		{
			if (!Watchers.ContainsKey(nome))
				Watchers[nome] = new List<Action<object>>();

			Watchers[nome].Add(acao);
			print($"Watch '{nome}' ativo!");
		}


		private static readonly Dictionary<string, object> Globals = new Dictionary<string, object>();

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



		public static void unwatch(string nome)
		{
			Watchers.Remove(nome);
			print($"Watch '{nome}' removido!");
		}
		public static void CSH()
		{
			Color(ConsoleColor.Cyan);
			clear();
			string meuId = GetUserId("CSH");
			print($"CSH v2.0 PYTHONIZADO\nID = [{meuId}]\n-- HELP | EXIT\n");
			while (true)
			{
				string linha = input<string>($"{_virtualCwd} $> ");
				if (new[] { "exit", "sair", "quit" }.Contains(linha.Trim().ToLower())) break;
				Interpretar(linha);
			}
		}

		public static void Interpretar(string linha)
		{
			if (string.IsNullOrWhiteSpace(linha)) return;
			string[] partes = linha.Trim().Split(' ', 2);
			string cmd = partes[0].ToUpper();
			string arg = partes.Length > 1 ? partes[1] : "";

			SwitchAction(cmd, new Dictionary<string, Action> {
				{ "HELP", () => print("ls, cd, rm, nano, var, hint, lc, calc, progress, chat, cls, beep") },
				{ "VAR", () => {
					var d = (IDictionary<string, object>)py;
					if (string.IsNullOrEmpty(arg)) {
						foreach (var v in d) print($"{v.Key} = {v.Value}");
					} else if (arg.Contains("=")) {
						var p = arg.Split('=');
						var(p[0].Trim(), p[1].Trim());
						print($"{p[0].Trim()} definido.");
					}
				}},
				{ "CLS", () => clear() },
				{ "LS", () => {
					try {
						foreach (var d in Directory.GetDirectories(_virtualCwd)) print($"repo. {Path.GetFileName(d)}/");
						foreach (var f in Directory.GetFiles(_virtualCwd)) print($"arq. {Path.GetFileName(f)}");
					} catch { print("Erro de acesso."); }
				}},
				{ "CALC", () => { try { print(new DataTable().Compute(arg, null)); } catch { print("Erro"); } } }
			}, () => print("Comando inválido."));
		}


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

		public static void SendSignal(string s, string v)
		{
			try { Directory.CreateDirectory(_signalDir); File.WriteAllText(Path.Combine(_signalDir, $"signal_{s}.txt"), v); } catch { }
		}

		public static string CheckSignal(string s)
		{
			string p = Path.Combine(_signalDir, $"signal_{s}.txt");
			return File.Exists(p) ? File.ReadAllText(p) : null;
		}

		public static bool WaitSignal(string s, int t)
		{
			var sw = Stopwatch.StartNew();
			while (sw.ElapsedMilliseconds < t) { if (CheckSignal(s) != null) return true; Thread.Sleep(100); }
			return false;
		}

		public static string GetUserId(string app = "CSIGN")
		{
			string id = LoadConfig($"id_{app}");
			if (string.IsNullOrEmpty(id)) { id = Guid.NewGuid().ToString()[..8]; SaveConfig($"id_{app}", id); }
			return id;
		}

		public static void ChatP2P()
		{
			string me = GetUserId();
			print($"ID: {me} | [ID] [MSG] ou 'sair'");
			while (true)
			{
				string e = input<string>("CHAT> ");
				if (e == "sair") break;
				string[] p = e.Split(' ', 2);
				if (p.Length == 2) SendSignal($"MSG_{p[0]}_FROM_{me}", p[1]);
			}
		}

		public static void MiniServer()
		{
			try
			{
				var l = new System.Net.HttpListener(); l.Prefixes.Add("http://localhost:8080/"); l.Start();
				print("Servidor em http://localhost:8080/");
				while (true)
				{
					var c = l.GetContext(); byte[] b = Encoding.UTF8.GetBytes($"CSIGN: {DateTime.Now}");
					c.Response.OutputStream.Write(b, 0, b.Length); c.Response.Close();
				}
			}
			catch (Exception ex) { print(ex.Message); }
		}
		public static dynamic def(string nome, Func<object[], object> func)
		{
			py[nome] = func;
			print($"def {nome}()");
			return func;
		}
		public static string f(string fmt, params object[] args) =>
	string.Format(fmt.Replace("{{", "{").Replace("}}", "}"), args);
	}
}