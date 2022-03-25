using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using System.Diagnostics;
using System.ComponentModel;

namespace Move_to_SSD {
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window {
		private List<Game> gameList = new List<Game>();
		long totalSize = 0;

		public MainWindow() {
			InitializeComponent();
			var f = File.OpenWrite("CACHE.txt");
			f.Close();

			string[] SSDs = File.ReadAllLines("SSDs.txt");
			string[] paths = File.ReadAllLines("PATHs.txt");

			SelectSSD.ItemsSource = SSDs;
			SelectSSD.SelectedIndex = 0;

			List<string> gamesList = new List<string>();
			foreach (var path in paths) {
				if (Directory.Exists(path)) {
					var games = Directory.EnumerateDirectories(path).ToList();
					gamesList.AddRange(games);
				}
			}

			ScanGames(gamesList);
		}

		private void filterGames() {
			gameHDD.Items.Clear();
			foreach (var game in gameList) {
				if (Search.Text == "") {
					gameHDD.Items.Add(game);
					continue;
				}
				string[] searchTerms = Search.Text.ToLower().Split(' ');
				string gameText = game.name.ToLower();
				int matched = 0;
				foreach (var term in searchTerms) {
					if (gameText.Contains(term)) {
						matched++;
					} else {
						break;
					}
				}
				if (searchTerms.Length == matched) {
					gameHDD.Items.Add(game);
				}
			}
		}

		void size_DoWork(object sender, DoWorkEventArgs e) {
			var arg = (sizeArgument)e.Argument;
			var gamesList = arg.gamesList;
			var cache = arg.cache;
			var ssd = arg.ssd;
			foreach (var game in gamesList) {
				long size = -1;
				DirectoryInfo di = new DirectoryInfo(game);
				var time = di.LastWriteTimeUtc;
				var timeString = time.ToString();
				for (int i = 0; i < cache.Length; i += 3) {
					if (cache[i] == game && cache[i + 1] == timeString) {
						size = long.Parse(cache[i + 2]);
					}
				}

				string toCache = "";
				if (size == -1) {
					Console.WriteLine(game);
					size = DirectorySize(game);
					toCache = game + "\r\n" + timeString + "\r\n" + size + "\r\n";
					(sender as BackgroundWorker).ReportProgress(1, new sizeProgress(game, size, ssd, toCache));
				} else if (size == 0) {
					Directory.Delete(game, true);
				} else {
					(sender as BackgroundWorker).ReportProgress(0, new sizeProgress(game, size, ssd, toCache));
				}
			}
		}

		void size_ProgressChanged(object sender, ProgressChangedEventArgs e) {
			var state = (sizeProgress)e.UserState;

			Game g = new Game(state.game, state.size);
			if (state.ssd) {
				gameSSD.Items.Add(g);
			} else {
				gameList.Add(g);
				totalSize += state.size;

				scanProgress.Value = gameList.Count;
				gameCount.Content = "Total Count: " + gameList.Count;
				gameSize.Content = "Total Size: " + gbSize(totalSize);
				if (e.ProgressPercentage == 1) {
					sortGames();
				}
			}

			if (state.toCache != "") {
				File.AppendAllText("CACHE.txt", state.toCache);
			}
		}

		void size_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
			scanProgress.Value = 0;
			sortGames();
		}

		public void ScanGames(List<string> gamesList, bool ssd = false) {
			var cache = File.ReadAllLines("CACHE.txt");

			scanProgress.Maximum = gamesList.Count;

			BackgroundWorker sizeWorker = new BackgroundWorker();
			sizeWorker.WorkerReportsProgress = true;
			sizeWorker.DoWork += size_DoWork;
			sizeWorker.ProgressChanged += size_ProgressChanged;
			sizeWorker.RunWorkerCompleted += size_RunWorkerCompleted;
			sizeWorker.RunWorkerAsync(new sizeArgument(gamesList, cache, ssd));
		}

		public long DirectorySize(string path) {
			long size = 0;
			try {
				var dirs = Directory.EnumerateDirectories(path);
				foreach (var dir in dirs) {
					size += DirectorySize(dir);
				}

				var files = Directory.EnumerateFiles(path);
				foreach (var file in files) {
					var fi = new FileInfo(file);
					size += fi.Length;
				}
			} catch (Exception e) {
			}
			return size;
		}

		private void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs, object sender) {
			// Get the subdirectories for the specified directory.
			DirectoryInfo dir = new DirectoryInfo(sourceDirName);

			if (!dir.Exists) {
				throw new DirectoryNotFoundException(
					"Source directory does not exist or could not be found: "
					+ sourceDirName);
			}

			DirectoryInfo[] dirs = dir.GetDirectories();

			// If the destination directory doesn't exist, create it.       
			Directory.CreateDirectory(destDirName);

			// Get the files in the directory and copy them to the new location.
			FileInfo[] files = dir.GetFiles();
			foreach (FileInfo file in files) {
				string tempPath = System.IO.Path.Combine(destDirName, file.Name);
				file.CopyTo(tempPath, false);
				(sender as BackgroundWorker).ReportProgress((int)file.Length);
			}

			// If copying subdirectories, copy them and their contents to new location.
			if (copySubDirs) {
				foreach (DirectoryInfo subdir in dirs) {
					string tempPath = System.IO.Path.Combine(destDirName, subdir.Name);
					DirectoryCopy(subdir.FullName, tempPath, copySubDirs, sender);
				}
			}
		}

		public static string gbSize(long size) {
			var sizeF = size / (double)1000000;
			if (sizeF > 1000)
				return Math.Round(sizeF / 1000, 2) + "GB";
			if (sizeF > 10)
				return Math.Round(sizeF, 0) + "MB";
			return Math.Round(sizeF, 1) + "MB";
		}

		private void updateSSD(string d, bool onlySize = false) {
			if (!Directory.Exists(d + ":\\")) {
				diskFree.Content = "Free Space: 0GB";
				diskTotal.Content = "Total Space: 0GB";
				return;
			}
			Directory.CreateDirectory(d + @":\Games");
			if (!onlySize) {
				gameSSD.Items.Clear();
				List<string> gamesList = new List<string>();
				gamesList.AddRange(Directory.EnumerateDirectories(d + @":\Games"));
				ScanGames(gamesList, true);
			}

			long SSDfreespace = 0;
			long SSDspace = 0;
			DriveInfo drive = new DriveInfo(d);
			if (drive.IsReady) {
				SSDfreespace = drive.AvailableFreeSpace;
				SSDspace = drive.TotalSize;
			} else {
				return;
			}

			diskFree.Content = "Free Space: " + gbSize(SSDfreespace) + "GB";
			diskTotal.Content = "Total Space: " + gbSize(SSDspace) + "GB";
		}

		private void SelectSSD_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			updateSSD(e.AddedItems[0].ToString());
		}

		private void sortGames() {
			if (sortButton.Content.ToString() == "Sort by Name") {
				gameList.Sort(delegate (Game x, Game y) { 
					if (x.size < y.size) return 1;
					else if (x.size == y.size) return 0;
					else return -1;
				});
			} else {
				gameList.Sort((Game x, Game y) => String.Compare(x.ToString(), y.ToString()));
			}
			filterGames();
		}
		private void sortButton_Click(object sender, RoutedEventArgs e) {
			if (sortButton.Content.ToString() == "Sort by Size") {
				sortButton.Content = "Sort by Name";
			} else {
				sortButton.Content = "Sort by Size";
			}
			sortGames();
		}

		private void Search_TextChanged(object sender, TextChangedEventArgs e) {
			filterGames();
		}

		void worker_DoWork(object sender, DoWorkEventArgs e) {
			string[] paths = (string[])e.Argument;
			var path = paths[0];
			var gameDir = paths[1];

			DirectoryCopy(path, gameDir, true, sender);
			e.Result = e.Argument;
		}

		void worker_ProgressChanged(object sender, ProgressChangedEventArgs e) {
			copyProgress.Value += e.ProgressPercentage;
		}

		void worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
			string[] paths = (string[])e.Result;
			var path = paths[0];
			var gameDir = paths[1];

			DirectoryInfo di = new DirectoryInfo(gameDir);
			File.WriteAllText(gameDir + ".txt", path + "\r\n" + di.LastAccessTimeUtc);
			Directory.Move(path, path + "-HDD");

			var psi = new ProcessStartInfo("cmd.exe", " /C mklink /D \"" + path + "\" \"" + gameDir + "\"");
			psi.CreateNoWindow = true;
			psi.UseShellExecute = false;
			Process.Start(psi).WaitForExit();

			updateSSD(SelectSSD.Text);
			copyProgress.Value = 0;
			moveHDD.IsEnabled = true;
			moveSSD.IsEnabled = true;
			SelectSSD.IsEnabled = true;
		}

		private void moveSSD_Click(object sender, RoutedEventArgs e) {
			var currentGame = (Game)gameHDD.SelectedItem;
			var gameDir = System.IO.Path.GetFileName(currentGame.path);
			gameDir = SelectSSD.Text + @":\Games\" + gameDir;

			DriveInfo drive = new DriveInfo(SelectSSD.Text);
			if (drive.IsReady) {
				if (drive.AvailableFreeSpace < currentGame.size) {
					MessageBox.Show("Not enough space available!!!");
					return;
				}
			} else {
				MessageBox.Show("Drive not ready!!!");
				return;
			}

			copyProgress.Maximum = currentGame.size;
			moveHDD.IsEnabled = false;
			moveSSD.IsEnabled = false;
			SelectSSD.IsEnabled = false;

			BackgroundWorker worker = new BackgroundWorker();
			worker.WorkerReportsProgress = true;
			worker.DoWork += worker_DoWork;
			worker.ProgressChanged += worker_ProgressChanged;
			worker.RunWorkerCompleted += worker_RunWorkerCompleted;
			string[] paths = { currentGame.path, gameDir };
			worker.RunWorkerAsync(paths);
		}

		private void moveHDD_Click(object sender, RoutedEventArgs e) {
			var currentGame = (Game)gameSSD.SelectedItem;
			var path = currentGame.path;

			var data = File.ReadAllLines(path + ".txt");
			string hddPath = data[0];
			string timeOld = data[1];

			DirectoryInfo diSSD = new DirectoryInfo(path);
			string timeNew = diSSD.LastWriteTimeUtc.ToString();

			if (Directory.Exists(hddPath))
				Directory.Delete(hddPath, true);
			if (timeNew == timeOld) {
				Directory.Move(hddPath + "-HDD", hddPath);
			} else {
				UpdateHDD(hddPath, timeOld, diSSD);
				Directory.Delete(hddPath + "-HDD", true);
			}
			File.Delete(path + ".txt");
			Directory.Delete(path, true);
			updateSSD(SelectSSD.Text);
		}

		private void UpdateHDD(string hddPath, string timeOld, DirectoryInfo diSSD) {
			string gameName = hddPath.Substring(hddPath.LastIndexOf('\\') + 1);
			string pathName = diSSD.FullName.Substring(9).Replace(gameName, "");
			string destination = hddPath + pathName;
			Directory.CreateDirectory(destination);
			foreach (var dir in diSSD.EnumerateDirectories()) {
				string dirName = dir.FullName.Replace(dir.FullName.Substring(0, 9) + gameName, "");
				if (String.Compare(dir.LastWriteTimeUtc.ToString(), timeOld) > 0) {
					UpdateHDD(hddPath, timeOld, dir);
				} else {
					string from = hddPath + "-HDD" + dirName;
					string to = hddPath + dirName;
					Directory.Move(from, to);
				}
			}
			foreach (var file in diSSD.EnumerateFiles()) {
				string fileName = file.FullName.Replace(file.FullName.Substring(0, 9) + gameName, "");
				if (String.Compare(file.LastWriteTimeUtc.ToString(), timeOld) > 0) {
					string from = file.FullName;
					string to = hddPath + fileName;
					File.Copy(from, to);
				} else {
					string from = hddPath + "-HDD" + fileName;
					string to = hddPath + fileName;
					File.Move(from, to);
				}
			}
		}
	}

	public class sizeProgress {
		public long size;
		public string toCache;
		public bool ssd;
		public string game;

		public sizeProgress(string game, long size, bool ssd, string toCache) {
			this.game = game;
			this.size = size;
			this.ssd = ssd;
			this.toCache = toCache;
		}
	}
	public class sizeArgument {
		public List<string> gamesList;
		public string[] cache;
		public bool ssd;

		public sizeArgument(List<string> gamesList, string[] cache, bool ssd) {
			this.gamesList = gamesList;
			this.cache = cache;
			this.ssd = ssd;
		}
	}

	public class Game {
		public string name;
		public string path;
		public long size;

		public Game(string path, long size) {
			name = System.IO.Path.GetFileName(path);
			this.path = path;
			this.size = size;
		}

		public override string ToString() {
			StringBuilder sb = new StringBuilder();
			bool capital = true;
			foreach (var c in name) {
				if (capital) {
					sb.Append(c.ToString().ToUpper());
					capital = false;
				} else {
					sb.Append(c.ToString().ToLower());
					if (c == ' ')
						capital = true;
				}
			}
			return sb.ToString() + "    " + MainWindow.gbSize(size);
		}
	}
}
