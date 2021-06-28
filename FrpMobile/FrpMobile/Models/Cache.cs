using System;
using System.Collections.Generic;
using System.Text;
using SQLite;

namespace FrpMobile.Models
{
    public class Cache
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string CurAddress { get; set; }
        public string Token { get; set; }
        public DateTime dateTime { get; set; }
    }
}
