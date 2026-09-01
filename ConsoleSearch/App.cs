using System;
using System.Linq;

namespace ConsoleSearch
{
    public class App
    {

        public void Run()
        {
            IDatabase db = new DatabaseSqlite();
            SearchLogic mSearchLogic = new SearchLogic(db);
            Console.WriteLine("Console Search");

            while (true)
            {
                Console.WriteLine("enter search terms - q for quit");
                string? input = Console.ReadLine();
                if (input == null || input.Equals("q")) break;

                var tokens = input.Split(" ", StringSplitOptions.RemoveEmptyEntries);
                bool caseSensitive = tokens.Contains("--case");
                var query = tokens.Where(t => t != "--case").ToArray();

                var result = mSearchLogic.Search(query, caseSensitive);

                if (result.Ignored.Count > 0)
                {
                    Console.WriteLine($"Ignored: {string.Join(',', result.Ignored)}");
                }

                int idx = 1;
                foreach (var doc in result.DocumentHits)
                {
                    Console.WriteLine($"{idx} : {doc.Document.mUrl} -- contains {doc.NoOfHits} search terms");
                    Console.WriteLine("Index time: " + doc.Document.mIdxTime);
                    Console.WriteLine($"Missing: {ArrayAsString(doc.Missing.ToArray())}");
                    idx++;
                }
                Console.WriteLine("Documents: " + result.Hits + ". Time: " + result.TimeUsed.TotalMilliseconds);
            }
        }

        string ArrayAsString(string[] s) => s.Length == 0 ? "[]" : $"[{String.Join(',', s)}]";
    }
}
