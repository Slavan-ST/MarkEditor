using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZPLEditor.Models.Enums;

namespace ZPLEditor.Models
{
    public class LabelElement
    {
        public Control Control { get; set; }
        public ElementType Type { get; set; }
        public byte[]? Data { get; set; }

        private string _name = string.Empty;
        public string Name
        {
            get
            {
                if (_name == string.Empty)
                {
                    return Guid.NewGuid().ToString();
                }
                else
                {
                    return _name;
                }

            }

            set
            {
                _name = value;
            }
        }
    }
}
