using FrpMobile.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SQLite;
using System.IO;

namespace FrpMobile.Services
{
    public class DataStore
    {
        readonly SQLiteAsyncConnection database;
        public DataStore()
        {
            database = new SQLiteAsyncConnection(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "server.db3"));
            database.CreateTablesAsync<Item,Cache>().Wait();
        }
        public DataStore(string path)
        {
            database = new SQLiteAsyncConnection(path);
            database.CreateTablesAsync<Item,Cache>().Wait();
        }
        public Task<int> AddItemAsync(Item item)
        {
            return database.InsertAsync(item);
        }

        public Task<int> UpdateItemAsync(Item item)
        {
            return database.UpdateAsync(item);
        }

        public Task<int> DeleteItemAsync(int id)
        {
            return database.DeleteAsync(new Item() { Id=id});
        }

        public Task<Item> GetItemAsync(string id)
        {
            return database.GetAsync<Item>(id);
        }

        public Task<List<Item>> GetAllItemsAsync()
        {
            return database.Table<Item>().ToListAsync();
        }
        public Task<Cache> GetCache()
        {
            return database.GetAsync<Cache>(1);
        }
        public Task<int> UpdataCache(Cache cache)
        {
            return database.UpdateAsync(cache);
        }
        public Task<int> AddCache(Cache cache)
        {
            return database.InsertAsync(cache);
        }
    }
}