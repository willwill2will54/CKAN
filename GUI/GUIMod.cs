using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace CKAN
{
    public sealed class GUIMod
    {
        private Module Mod { get; set; }

        public string Name
        {
            get { return Mod.name.Trim(); }
        }

        public bool IsInstalled { get; private set; }
        public bool HasUpdate { get; private set; }
        public bool IsIncompatible { get; private set; }
        public bool IsAutodetected { get; private set; }
        public string Authors { get; private set; }
        public string InstalledVersion { get; private set; }
        public string LatestVersion { get; private set; }

        // These indicate the maximum KSP version that the maximum available
        // version of this mod can handle. The "Long" version also indicates
        // to the user if a mod upgrade would be required. (#1270)
        public string KSPCompatibility { get; private set; }
        public string KSPCompatibilityLong { get; private set; }

        public string KSPversion { get; private set; }
        public string Abstract { get; private set; }
        public string Homepage { get; private set; }
        public string Identifier { get; private set; }
        public bool IsInstallChecked { get; set; }
        public bool IsUpgradeChecked { get; private set; }
        public bool IsNew { get; set; }
        public bool IsCKAN { get; private set; }

        public string Version
        {
            get { return IsInstalled ? InstalledVersion : LatestVersion; }
        }

        public GUIMod(Module mod, IRegistryQuerier registry, KSPVersion current_ksp_version)
        {
            IsCKAN = mod is CkanModule;
            //Currently anything which could alter these causes a full reload of the modlist
            // If this is ever changed these could be moved into the properties
            Mod = mod;
            IsInstalled = registry.IsInstalled(mod.identifier, false);
            IsInstallChecked = IsInstalled;
            HasUpdate = registry.HasUpdate(mod.identifier, current_ksp_version);
            IsIncompatible = !mod.IsCompatibleKSP(current_ksp_version);
            IsAutodetected = registry.IsAutodetected(mod.identifier);
            Authors = mod.author == null ? "N/A" : String.Join(",", mod.author);

            var installed_version = registry.InstalledVersion(mod.identifier);
            Version latest_version = null;
            var ksp_version = mod.ksp_version;

            try
            {
                var latest_available = registry.LatestAvailable(mod.identifier, current_ksp_version);
                if (latest_available != null)
                    latest_version = latest_available.version;
            }
            catch (ModuleNotFoundKraken)
            {
                latest_version = installed_version;
            }

            InstalledVersion = installed_version != null ? installed_version.ToString() : "-";

            // Let's try to find the compatibility for this mod. If it's not in the registry at
            // all (because it's a DarkKAN mod) then this might fail.

            CkanModule latest_available_for_any_ksp = null;

            try
            {
                latest_available_for_any_ksp = registry.LatestAvailable(mod.identifier, null);
            }
            catch
            {
                // If we can't find the mod in the CKAN, but we've a CkanModule installed, then
                // use that.
                if (IsCKAN)
                    latest_available_for_any_ksp = (CkanModule) mod;
                
            }

            // If there's known information for this mod in any form, calculate the highest compatible
            // KSP.
            if (latest_available_for_any_ksp != null)
            {
                KSPCompatibility = KSPCompatibilityLong = latest_available_for_any_ksp.HighestCompatibleKSP();

                // If the mod we have installed is *not* the mod we have installed, or we don't know
                // what we have installed, indicate that an upgrade would be needed.
                if (installed_version == null || !latest_available_for_any_ksp.version.IsEqualTo(installed_version))
                {
                    KSPCompatibilityLong = string.Format("{0} (using mod version {1})",
                        KSPCompatibility, latest_available_for_any_ksp.version);
                }
            }
            else
            {
                // No idea what this mod is, sorry!
                KSPCompatibility = KSPCompatibilityLong = "unknown";
            }

            if (latest_version != null)
            {
                LatestVersion = latest_version.ToString();
            }
            else if (latest_available_for_any_ksp != null)
            {
                LatestVersion = latest_available_for_any_ksp.version.ToString();
            }
            else
            {
                LatestVersion = "-";
            }

            KSPversion = ksp_version != null ? ksp_version.ToString() : "-";

            Abstract = mod.@abstract;
            
            // If we have homepage provided use that, otherwise use the kerbalstuff page or the github repo so that users have somewhere to get more info than just the abstract.

            Homepage = "N/A";
            if (mod.resources != null)
            {
                if (mod.resources.homepage != null)
                {
                    Homepage = mod.resources.homepage.ToString();
                }
                else if (mod.resources.kerbalstuff != null)
                {
                    Homepage = mod.resources.kerbalstuff.ToString();
                }
                else if (mod.resources.repository != null)
                {
                    Homepage = mod.resources.repository.ToString();
                }
            }

            Identifier = mod.identifier;
        }

        public GUIMod(CkanModule mod, IRegistryQuerier registry, KSPVersion current_ksp_version)
            : this((Module) mod, registry, current_ksp_version)
        {
        }

        public CkanModule ToCkanModule()
        {
            if (!IsCKAN) throw new InvalidCastException("Method can not be called unless IsCKAN");
            var mod = Mod as CkanModule;
            return mod;
        }

        public Module ToModule()
        {
            return Mod;
        }

        public KeyValuePair<GUIMod, GUIModChangeType>? GetRequestedChange()
        {
            if (IsInstalled ^ IsInstallChecked)
            {
                var change_type = IsInstalled ? GUIModChangeType.Remove : GUIModChangeType.Install;
                return new KeyValuePair<GUIMod, GUIModChangeType>(this, change_type);
            }
            if (IsInstalled && (IsInstallChecked && HasUpdate && IsUpgradeChecked))
            {
                return new KeyValuePair<GUIMod, GUIModChangeType>(this, GUIModChangeType.Update);
            }
            return null;
        }

        public static implicit operator Module(GUIMod mod)
        {
            return mod.ToModule();
        }

        public void SetUpgradeChecked(DataGridViewRow row, bool? set_value_to = null)
        {
            //Contract.Requires<ArgumentException>(row.Cells[1] is DataGridViewCheckBoxCell);
            var update_cell = row.Cells[1] as DataGridViewCheckBoxCell;
            var old_value = (bool) update_cell.Value;

            bool value = set_value_to ?? old_value;
            IsUpgradeChecked = value;
            if (old_value != value) update_cell.Value = value;
        }

        public void SetInstallChecked(DataGridViewRow row, bool? set_value_to = null)
        {
            //Contract.Requires<ArgumentException>(row.Cells[0] is DataGridViewCheckBoxCell);
            var install_cell = row.Cells[0] as DataGridViewCheckBoxCell;
            bool changeTo = set_value_to != null ? (bool)set_value_to : (bool)install_cell.Value;
            //Need to do this check here to prevent an infite loop
            //which is at least happening on Linux
            //TODO: Elimate the cause
            if (changeTo != IsInstallChecked)
            {
                IsInstallChecked = changeTo;
                install_cell.Value = IsInstallChecked;
            }
        }


        private bool Equals(GUIMod other)
        {
            return Equals(Name, other.Name);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((GUIMod) obj);
        }

        public override int GetHashCode()
        {
            return (Name != null ? Name.GetHashCode() : 0);
        }
    }
}
