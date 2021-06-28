using System;
using SQLite;

namespace FrpMobile.Models
{
    public class Item
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string Address { get; set; }

        public string Token { get; set; }
        public string Description { get; set; }
    }
}