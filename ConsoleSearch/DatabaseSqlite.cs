using System;
using System.Collections.Generic;
using Shared;
using Shared.Model;
using Microsoft.Data.Sqlite;

namespace ConsoleSearch
{
    public class DatabaseSqlite : IDatabase
    {
        private SqliteConnection _connection;

        public DatabaseSqlite()
        {
            var connectionStringBuilder = new SqliteConnectionStringBuilder();

            connectionStringBuilder.DataSource = Paths.SQLITE_DATABASE;


            _connection = new SqliteConnection(connectionStringBuilder.ConnectionString);

            _connection.Open();


        }


        // key is the id of the document, the value is number of search words in the document
        public List<KeyValuePair<int, int>> GetDocuments(List<int> wordIds)
        {
            var res = new List<KeyValuePair<int, int>>();

            /* Example sql statement looking for doc id's that
               contain words with id 2 and 3
            
               SELECT docId, COUNT(wordId) as count
                 FROM Occ
                WHERE wordId in (2,3)
             GROUP BY docId
             ORDER BY COUNT(wordId) DESC 
             */

            var sql = "SELECT docId, COUNT(wordId) as count FROM Occ where ";
            sql += "wordId in " + AsString(wordIds) + " GROUP BY docId ";
            sql += "ORDER BY count DESC;";

            var selectCmd = _connection.CreateCommand();
            selectCmd.CommandText = sql;

            using (var reader = selectCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var docId = reader.GetInt32(0);
                    var count = reader.GetInt32(1);

                    res.Add(new KeyValuePair<int, int>(docId, count));
                }
            }

            return res;
        }

        private string AsString(IEnumerable<int> x) => $"({string.Join(',', x)})";





        public IReadOnlyDictionary<int, BEDocument> GetDocDetails(IReadOnlyList<int> docIds)
        {
            var res = new Dictionary<int, BEDocument>();
            if (docIds.Count == 0)
                return res;

            var selectCmd = _connection.CreateCommand();
            selectCmd.CommandText =
                $"SELECT id, url, idxTime, creationTime FROM document WHERE id IN {AsString(docIds)}";

            using var reader = selectCmd.ExecuteReader();
            while (reader.Read())
            {
                var id = reader.GetInt32(0);
                res[id] = new BEDocument
                {
                    mId = id,
                    mUrl = reader.GetString(1),
                    mIdxTime = reader.GetString(2),
                    mCreationTime = reader.GetString(3),
                };
            }
            return res;
        }

        public IReadOnlyDictionary<int, List<string>> GetMissingWords(
            IReadOnlyList<int> docIds, IReadOnlyList<int> wordIds)
        {
            var res = new Dictionary<int, List<string>>();
            if (docIds.Count == 0)
                return res;

            // query word id -> name
            var names = new Dictionary<int, string>();
            var nameCmd = _connection.CreateCommand();
            nameCmd.CommandText = $"SELECT id, name FROM word WHERE id IN {AsString(wordIds)}";
            using (var reader = nameCmd.ExecuteReader())
                while (reader.Read())
                    names[reader.GetInt32(0)] = reader.GetString(1);

            // which query words each document actually has
            var present = new Dictionary<int, HashSet<int>>();
            var occCmd = _connection.CreateCommand();
            occCmd.CommandText =
                $"SELECT docId, wordId FROM Occ WHERE docId IN {AsString(docIds)} AND wordId IN {AsString(wordIds)}";
            using (var reader = occCmd.ExecuteReader())
                while (reader.Read())
                {
                    var docId = reader.GetInt32(0);
                    if (!present.TryGetValue(docId, out var has))
                        present[docId] = has = new HashSet<int>();
                    has.Add(reader.GetInt32(1));
                }

            foreach (var docId in docIds)
            {
                present.TryGetValue(docId, out var has);
                var missing = new List<string>();
                foreach (var wordId in wordIds)
                    if (has == null || !has.Contains(wordId))
                        missing.Add(names[wordId]);
                res[docId] = missing;
            }
            return res;
        }

        public List<int> GetWordIds(string[] query, out List<string> outIgnored)
        {
            var res = new List<int>();
            outIgnored = new List<string>();

            var selectCmd = _connection.CreateCommand();
            selectCmd.CommandText = "SELECT id FROM word WHERE name = @name";
            var nameParam = selectCmd.CreateParameter();
            nameParam.ParameterName = "name";
            selectCmd.Parameters.Add(nameParam);

            foreach (var aWord in query)
            {
                nameParam.Value = TextNormalizer.Fold(aWord);
                var id = selectCmd.ExecuteScalar();

                if (id != null)
                    res.Add(Convert.ToInt32(id));
                else
                    outIgnored.Add(aWord);
            }
            return res;
        }
    }
}
