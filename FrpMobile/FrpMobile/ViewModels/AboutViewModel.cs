using System;
using System.Windows.Input;
using Xamarin.Essentials;
using Xamarin.Forms;
using FrpMobile.Services;
using FrpMobile.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FrpMobile.ViewModels
{
    public class AboutViewModel : BaseViewModel
    {
        private string _Address;
        private bool _status;
        private string _buttonText;
        private Command _connect;
        public Command Connect { 
            get => _connect;
            set => SetProperty(ref _connect,value);
        }
        public string ButtonText { 
            get => _buttonText;
            set => SetProperty(ref _buttonText, value);
        }
        public string CurAddress { 
            get => _Address;
            set => SetProperty(ref _Address,value);
        }
        public bool Status { 
            get => _status;
            set {
                SetProperty(ref _status, value);
                if (value == true)
                {
                    ButtonText = "Disconnect";
                    Connect = new Command(Disconnect);
                }
                else 
                {
                    ButtonText = "Connect";
                    Connect = new Command(ConnectServer, Validate);
                }
            }
        }
        public Alert alert;
        public SSL Connection;
        public AboutViewModel()
        {
            Title = "About";
            ButtonText = "Connect";
            Connect = new Command(ConnectServer,Validate);
            CurServer.setAddress = SetAddress;
            this.PropertyChanged +=
                (_, __) => Connect.ChangeCanExecute();
            if (CurServer.CurAddress != null) CurAddress = CurServer.CurAddress;
        }
        private void ConnectServer()
        {
            Connection?.Dispose();
            string address = CurAddress.Split(':')[0];
            int port = int.Parse(CurAddress.Split(':')[1]);
            Connection = new SSL(address, port,(x)=> { this.Status = x; },ref alert);
            if(!Connection.Init())
                Connection.Dispose();
        }
        private void Disconnect()
        {
            Connection.Dispose();
        }
        private bool Validate()
        {
            return !string.IsNullOrWhiteSpace(CurServer.CurAddress) && !string.IsNullOrWhiteSpace(CurServer.CurToken);
        }
        private void SetAddress(string address)
        {
            CurAddress = address;
        }
    }
}