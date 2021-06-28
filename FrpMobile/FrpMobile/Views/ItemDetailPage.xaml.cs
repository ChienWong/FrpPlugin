using FrpMobile.ViewModels;
using System.ComponentModel;
using Xamarin.Forms;

namespace FrpMobile.Views
{
    public partial class ItemDetailPage : ContentPage
    {
        public ItemDetailPage()
        {
            InitializeComponent();
            BindingContext = new ItemDetailViewModel();
        }
    }
}