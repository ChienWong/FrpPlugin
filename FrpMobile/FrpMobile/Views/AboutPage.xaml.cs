using FrpMobile.ViewModels;
using FrpMobile.Services;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace FrpMobile.Views
{
    public partial class AboutPage : ContentPage
    {
        public AboutPage()
        {
            InitializeComponent();
            AboutViewModel aboutViewModel = new AboutViewModel
            {
                alert = new Alert(IsAllow)
            };
            BindingContext = aboutViewModel;
        }
        protected override void OnAppearing()
        {
            base.OnAppearing();
        }
        public async Task<bool> IsAllow(string message)
        {
            bool answer = await DisplayAlert("Connection", message, "Yes", "No");
            return answer;
        } 
    }
}