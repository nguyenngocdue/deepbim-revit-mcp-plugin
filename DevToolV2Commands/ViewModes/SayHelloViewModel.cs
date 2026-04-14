using DevTooV2lCommands.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DevTooV2lCommands.ViewModes
{
    public class SayHelloViewModel
    {
        private readonly SayHelloModel _sayHelloModel;
        public SayHelloViewModel(SayHelloModel sayHelloModel)
        {
            _sayHelloModel = sayHelloModel;
        }

        public string Result { get; private set; } = string.Empty;
        public bool IsSuccess { get; private set; }

        public void Run()
        {
            Result = $"Message from the model: {_sayHelloModel.Message}";
            IsSuccess = true ;
        }

    }
}
