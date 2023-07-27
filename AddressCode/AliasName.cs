using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AddressCode
{
	class AliasName
	{
		public string Name { get; set; }
		public string Alias { get; set; }

		public AliasName(string name, string alias)
		{
			Name = name;
			Alias = alias;
		}

		public JObject ToJson()
		{
			return new JObject()
			{
				{"Name", Name },
				{"Alias", Alias }
			};
		}
	}
}
