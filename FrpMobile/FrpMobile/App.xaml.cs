using FrpMobile.Models;
using FrpMobile.Services;
using FrpMobile.Views;
using FrpMobile.ViewModels;
using System;
using System.IO;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace FrpMobile
{
    public partial class App : Application
    {

        public App()
        {
            InitializeComponent();
            DataStore datastore = new DataStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "people.db3"));
            DependencyService.RegisterSingleton<DataStore>(datastore);
            MainPage = new AppShell();
        }

        protected override void OnStart()
        {
            DataStore dataStore = DependencyService.Get<DataStore>();
            Cache cache = null;
            try
            {
                cache = dataStore.GetCache().Result;
            }
            catch { }
            if (cache == null) return;
            CurServer.CurToken = cache.Token;
            CurServer.CurAddress = cache.CurAddress;
        }

        protected override void OnSleep()
        {
            DataStore dataStore = DependencyService.Get<DataStore>();
            Cache cache = null;
            try
            {
                cache = dataStore.GetCache().Result;
            }
            catch { }
            if (cache == null)
            {
                dataStore.AddCache(new Cache()
                {
                    CurAddress = CurServer.CurAddress,
                    Token = CurServer.CurToken,
                    dateTime=DateTime.Now
                });
            }
            else
            {
                dataStore.UpdataCache(new Cache()
                {
                    Id = 1,
                    CurAddress = CurServer.CurAddress,
                    Token= CurServer.CurToken,
                    dateTime=DateTime.Now
                });
            }
        }

        protected override void OnResume()
        {
        }
    }
}
