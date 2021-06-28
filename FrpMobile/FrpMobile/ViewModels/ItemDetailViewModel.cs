using FrpMobile.Models;
using FrpMobile.Services;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace FrpMobile.ViewModels
{
    [QueryProperty(nameof(ItemId), nameof(ItemId))]
    public class ItemDetailViewModel : BaseViewModel
    {
        private string itemId;
        private string address;
        private string token;
        private string description;
        public string Id { get; set; }

        public string Address
        {
            get => address;
            set => SetProperty(ref address, value);
        }
        public string Token
        {
            get => token;
            set => SetProperty(ref token, value);
        }
        public string Description
        {
            get => description;
            set => SetProperty(ref description, value);
        }

        public string ItemId
        {
            get
            {
                return itemId;
            }
            set
            {
                itemId = value;
                LoadItemId(value);
            }
        }
        public ItemDetailViewModel()
        {
            SaveCommand = new Command(Save, ValidateSave);
            DeleteCommand = new Command(Delete);
            SelectCommand = new Command(Select);
            this.PropertyChanged +=
                (_, __) => SaveCommand.ChangeCanExecute();
        }
        public Command SaveCommand { get; }
        public Command DeleteCommand { get; }
        public Command SelectCommand { get; }
        public async void LoadItemId(string itemId)
        {
            try
            {
                var item = await DataStore.GetItemAsync(itemId);
                Id = item.Id+"";
                Token = item.Token;
                Address = item.Address;
                Description = item.Description;
            }
            catch (Exception)
            {
                Debug.WriteLine("Failed to Load Item");
            }
        }
        private bool ValidateSave()
        {
            return !String.IsNullOrWhiteSpace(address)
                && !String.IsNullOrWhiteSpace(token);
        }
        private async void Save()
        {
            Item item = new Item()
            {
                Id = int.Parse(Id),
                Address = address,
                Token = token,
                Description = description
            };
            await DataStore.UpdateItemAsync(item);
            await Shell.Current.GoToAsync("..");
        }
        private async void Delete()
        {
            await DataStore.DeleteItemAsync(int.Parse(Id));
            await Shell.Current.GoToAsync("..");
        }
        private async void Select()
        {
            CurServer.CurToken = token;
            CurServer.CurAddress = address;
            await Shell.Current.GoToAsync($"//AboutPage");
        }
    }
}
