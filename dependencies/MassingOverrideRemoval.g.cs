using Elements;
using System.Collections.Generic;
using System;
using System.Linq;

namespace CreateEnvelopes
{
	/// <summary>
	/// Override metadata for MassingOverrideRemoval
	/// </summary>
	public partial class MassingOverrideRemoval : IOverride
	{
        public static string Name = "Massing Removal";
        public static string Dependency = null;
        public static string Context = "[*discriminator=Elements.Envelope]";
		public static string Paradigm = "Edit";

        /// <summary>
        /// Get the override name for this override.
        /// </summary>
        public string GetName() {
			return Name;
		}

		public object GetIdentity() {

			return Identity;
		}

	}

}