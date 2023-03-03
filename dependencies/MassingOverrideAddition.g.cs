using Elements;
using System.Collections.Generic;
using System;
using System.Linq;

namespace CreateEnvelopes
{
	/// <summary>
	/// Override metadata for MassingOverrideAddition
	/// </summary>
	public partial class MassingOverrideAddition : IOverride
	{
        public static string Name = "Massing Addition";
        public static string Dependency = null;
        public static string Context = "[*discriminator=Elements.ConceptualMass]";
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