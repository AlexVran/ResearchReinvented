using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PeteTimesSix.ResearchReinvented.Domain.Settings;

namespace PeteTimesSix.ResearchReinvented.Data
{
    public class CategorySettingsFinal : CategorySettingsPreset
    {
        public void Update(CategorySettingsPreset preset, CategorySettingsChanges changes)
        {
            var resolved = CategorySettingsResolver.Resolve(
                CategorySettingsDomainAdapter.ToValue(preset),
                CategorySettingsDomainAdapter.ToOverride(changes));
            CategorySettingsDomainAdapter.Apply(resolved, this);
        }
    }
}
