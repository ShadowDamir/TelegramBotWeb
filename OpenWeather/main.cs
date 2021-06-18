using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenWeather
{
    class main
    {
        private double _temp;

        public double temp
        {
            get
            {
                return _temp;
            }
            set
            {
                _temp = value - 273.15;
            }
        }
    }
}
