using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmvGenerator
{
    class State
    {
        public State()
        {
            Data = new Dictionary<string, string>();
        }

        public int EventId { get; set; }
        public string Name { get; set; }
        public Dictionary<string,string> Data { get; set; }
    }
}
