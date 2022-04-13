using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Linq;
using Articy.Api;
using Articy.Api.Plugins;

namespace ScenarioWorld.ArticyWwisePlugin
{
    // ReSharper disable once UnusedType.Global
    public class Plugin : MacroPlugin
    {
        public override string DisplayName => "Wwise Plugin";

        public override string ContextName => "Wwise";
        
        public override List<MacroCommandDescriptor> GetMenuEntries(List<ObjectProxy> aSelectedObjects, ContextMenuContext aContext)
        {
            var list = new List<MacroCommandDescriptor>();
            switch (aContext)
            {
                case ContextMenuContext.Global:
                    list.Add(new MacroCommandDescriptor
                        {
                            CaptionLid = "Sync Objects",
                            ModifiesData = true,
                            Execute = SyncWwiseEvents
                        }
                    );
                    if (!string.IsNullOrWhiteSpace(PluginSettings.Default.LastWwiseProject))
                    {
                        list.Add(new MacroCommandDescriptor
                            {
                                CaptionLid = "Sync Objects (use previous settings)",
                                ModifiesData = true,
                                Execute = SyncWwiseEventsUsePrevious
                            }
                        );
                    }
                    break;
            }

            return list;
        }

        private void SyncWwiseEventsUsePrevious(MacroCommandDescriptor descriptor, List<ObjectProxy> selectedObjects)
        {
            // Get all event names
            var eventNames = GetEventWorkUnits(PluginSettings.Default.LastWwiseProject).SelectMany(GetEvents).Distinct();

            var folder = Session.GetObjectById(PluginSettings.Default.LastEntityFolder);
            var template = Session.GetObjectById(PluginSettings.Default.LastEntityTemplate);

            // Create entities
            foreach (var eventName in eventNames)
                CreateWwiseEntity(eventName, folder, template);
        }

        private void SyncWwiseEvents(MacroCommandDescriptor descriptor, List<ObjectProxy> selectedObjects)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.InitialDirectory = @"C:\";
                dialog.Filter = "Wwise Projects (*.wproj)|*.wproj";
                dialog.RestoreDirectory = true;

                // Make sure we actually selected a file
                if (dialog.ShowDialog() != DialogResult.OK) return;
                
                // Get the folder to create all our entities within
                ObjectProxy folder = null;
                if (!Session.SelectObject(ref folder, obj => obj.IsFolder, false,
                        Session.GetSystemFolder(SystemFolderNames.Entities)))
                    return;
                
                // Get template for entities
                ObjectProxy template = null;
                if (!Session.SelectObject(ref template, obj => obj.ObjectType == ObjectType.Template, false,
                        Session.GetSystemFolder(SystemFolderNames.TemplateDesign)))
                    return;

                // Cache
                PluginSettings.Default.LastWwiseProject = dialog.FileName;
                PluginSettings.Default.LastEntityTemplate = template.Id;
                PluginSettings.Default.LastEntityFolder = folder.Id;
                PluginSettings.Default.Save();

                // Get all event names
                var eventNames = GetEventWorkUnits(dialog.FileName).SelectMany(GetEvents).Distinct();
                
                // Create entities
                foreach (var eventName in eventNames)
                    CreateWwiseEntity(eventName, folder, template);
            }
        }

        private void CreateWwiseEntity(string eventName, ObjectProxy parent, ObjectProxy template)
        {
            string technicalName = $"WWISE_{eventName}";
            var existing = Session.GetObjectByTechName(technicalName);
            if (existing == null)
            {
                var entity = Session.CreateEntity(parent, eventName, template.GetTechnicalName());
                entity.SetTechnicalName(technicalName);
            }
        }

        private IEnumerable<string> GetEvents(string workUnitFilename)
        {
            // Load xml file
            var xml = XDocument.Load(workUnitFilename);
            if(xml.Root == null)
                return Enumerable.Empty<string>();
            
            // Query it
            return (from e in xml.Root.Descendants("Event")
                select e.Attribute("Name")?.Value).Where(name => name != null);
        }

        private IEnumerable<string> GetEventWorkUnits(string projectFilename)
        {
            // Get project folder
            var projectFolder = Path.GetDirectoryName(projectFilename);
            if (projectFolder == null)
                return Enumerable.Empty<string>();

            // Get events folder
            var eventsFolder = Path.Combine(projectFolder, "Events");
            
            // Get all files in events folder
            return Directory.GetFiles(eventsFolder, "*.wwu");
        }
    }
}