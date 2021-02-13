using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SearchMyRiddles
{
   internal class PuzzleSearch
   {
      private List<PuzzleData> PuzzleSourceUrls { get; } = new List<PuzzleData>();
      private List<GameData> AllUserGames { get; } = new List<GameData>();
      private SortedSet<string> UserNames { get; } = new SortedSet<string>();
      private List<GameData> FoundGames { get; } = new List<GameData>();

      private const string PuzzleSourceFile = "lichess_db_puzzle.csv";
      private const string WhiteFileNamePattern = "*-white.pgn";
      private const string BlackFileNamePattern = "*-black.pgn";
      private const string ReadmeFile = "Readme.txt";
      private const string ChallengeLink = "https://lichess.org/training/";

      public void Start()
      {
         var startTime = DateTime.Now;
         Console.WriteLine($"Start time: {startTime.ToString("G")}");
         Console.WriteLine("Program directory: " + Directory.GetCurrentDirectory());
         Console.WriteLine("Reading in Puzzle Games");
         if (ReadInPuzzleSource())
         {
            Console.WriteLine("\r" + PuzzleSourceUrls.Count + " Puzzles found");
            Console.WriteLine("Reading in User Games");
            ReadInUserGames();
            if (AllUserGames.Count == 0)
            {
               Console.WriteLine("No user games found, please follow the instructions");
               ShowInstructions();
            }
            else
            {
               Console.Write(AllUserGames.Count + " games found from: ");
               foreach (var userName in UserNames)
                  Console.Write(userName + (userName != UserNames.Last() ? ", " : string.Empty));
               Console.WriteLine("\nSearch the riddles in games of you");
               Search4UserGames();
               Console.WriteLine("\r            ");
               Console.WriteLine("Search successful ended");
               switch (FoundGames.Count)
               {
                  case 0:
                     Console.WriteLine("No games found");
                     break;
                  case 1:
                     Console.WriteLine("One game found");
                     break;
                  default:
                     Console.WriteLine(FoundGames.Count + " games found, scroll up to see");
                     break;
               }
               var endTime = DateTime.Now;
               Console.WriteLine($"Start time: {endTime.ToString("G")}");
               var timeDiff = endTime - startTime;
               Console.WriteLine("Time needed: " + timeDiff);
            }
         }
         Console.ReadKey(false);
      }

      private void ReadInUserGames()
      {
         var currentdirectory = Directory.GetCurrentDirectory();
         var userGamesFilesWhite = Directory.GetFiles(currentdirectory, WhiteFileNamePattern);
         var userGamesFilesBlack = Directory.GetFiles(currentdirectory, BlackFileNamePattern);
         ExtractUserNames(userGamesFilesWhite, WhiteFileNamePattern);
         ExtractUserNames(userGamesFilesBlack, BlackFileNamePattern);
         var userFiles = userGamesFilesWhite.Concat(userGamesFilesBlack);
         var allPgn = new List<List<string>>();

         Parallel.ForEach(userFiles, userFile => ReadInAllGames(userFile, allPgn));
         Parallel.ForEach(allPgn, ExtractGameDataFromPgn);
      }

      private void ExtractGameDataFromPgn(List<string> pgn)
      {
         if (pgn == null)
            return;
         var game = new GameData();
         foreach (var property in typeof(GameData).GetProperties())
            property.SetValue(game, ExtractValue(pgn, "[" + property.Name + " "));

         lock (AllUserGames)
            AllUserGames.Add(game);
      }

      private static void ReadInAllGames(string userFile, List<List<string>> allPgn)
      {
         var file = new StreamReader(userFile);
         string line;
         var pgnData = new List<string>();
         while ((line = file.ReadLine()) != null)
         {
            if (line.Length == 0 && pgnData.Count > 0 && pgnData.Last()?.Length == 0)
            {
               lock (allPgn)
                  allPgn.Add(new List<string>(pgnData));
               pgnData.Clear();
            }
            else
               pgnData.Add(line);
         }
         file.Dispose();
      }

      private static string ExtractValue(IEnumerable<string> pgn, string token)
      {
         var lines = pgn.Where(item => item.StartsWith(token));
         var linesAsArray = lines as string[] ?? lines.ToArray();
         if (!linesAsArray.Any())
            return null;
         var line = linesAsArray.First();
         var tokenLength = token.Length;
         return line.Substring(tokenLength + 1, line.Length - tokenLength - 3);
      }

      private void ExtractUserNames(IEnumerable<string> userGamesFiles, string fileNamePattern)
      {
         foreach (var userGameFile in userGamesFiles.Select(Path.GetFileName))
            UserNames.Add(userGameFile.Substring(0, userGameFile.Length - fileNamePattern.Length + 1));
      }

      private bool ReadInPuzzleSource()
      {
         if (!File.Exists(PuzzleSourceFile))
            return ShowInstructions();

         var counter = 0;
         var lines = File.ReadLines(PuzzleSourceFile);
         Parallel.ForEach(lines, line =>
         {
            if (++counter % 1000 == 0)
               Console.Write("\r" + counter);
            //if (counter > 200000)
            //   break;
            //if (counter < 220000)
            //   continue;
            var parts = line.Split(',');
            if (parts.Length != 9)
            {
               Console.WriteLine("\nWrong Line in " + PuzzleSourceFile + ": " + line);
               return;
            }

            var puzzleData = new PuzzleData
            {
               ChallengeId = parts[0],
               WinningMoves = parts[2],
               Comment = parts[7],
               Link = parts[8]
            };
            var posNumbersign = puzzleData.Link.LastIndexOf('#');
            puzzleData.MoveNumber = puzzleData.Link.Substring(posNumbersign + 1);
            puzzleData.Link = puzzleData.Link.Substring(0, posNumbersign);
            lock (PuzzleSourceUrls)
               PuzzleSourceUrls.Add(puzzleData);
         });

         return true;
      }

      private static bool ShowInstructions()
      {
         if (!File.Exists(ReadmeFile))
         {
            Console.WriteLine(ReadmeFile + " is missing");
            return false;
         }
         var readmeFile = new StreamReader(ReadmeFile);
         Console.WriteLine(PuzzleSourceFile + " is still missing, follow the instructions below");
         string line;
         while ((line = readmeFile.ReadLine()) != null)
            Console.WriteLine(line);
         return false;
      }

      private void Search4UserGames()
      {
         var progressCounter = 0;
         Parallel.ForEach(AllUserGames, game =>
        {
           if (++progressCounter % 10 == 0)
              Console.Write("\r" + progressCounter);
           SearchForUserGame(game);
        });
      }

      private void SearchForUserGame(GameData game)
      {
         var foundPuzzleGame = PuzzleSourceUrls.FirstOrDefault(item => item.Link == game.Site);
         if (foundPuzzleGame != null)
         {
            Console.Write("\r          ");
            FoundGames.Add(game);
            Console.WriteLine("\nLink to the game: " + foundPuzzleGame.Link + "#" + foundPuzzleGame.MoveNumber);
            Console.WriteLine("Link to training: " + ChallengeLink + foundPuzzleGame.ChallengeId);

            foreach (var property in typeof(GameData).GetProperties())
            {
               if (!(property.GetValue(game) is string value))
                  continue;
               var name = property.Name.PadRight(16, ' ');
               Console.Write("\r" + name + ": " + value + "\n");
            }
            Console.WriteLine("Comment".PadRight(16, ' ') + ": " + foundPuzzleGame.Comment);
            Console.WriteLine("Winning Moves".PadRight(16, ' ') + ": " + foundPuzzleGame.WinningMoves);
         }
      }
   }
}