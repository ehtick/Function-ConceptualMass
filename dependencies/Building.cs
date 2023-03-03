using System.Collections.Generic;
using ConceptualMassFromModules;

namespace Elements
{
    public class Building : Element
    {
        public List<string> MassAddIds { get; set; } = new List<string>();

        public Building Update(BuildingInfoOverride edit, List<ConceptualMass> masses)
        {
            Name = edit.Value.Name;
            foreach (var mass in masses)
            {
                if (MassAddIds.Contains(mass.AddId))
                {
                    mass.Name = Name;
                }
            }
            return this;
        }
    }
}