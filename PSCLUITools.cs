using System;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Management.Automation.Runspaces;
using System.Collections.Generic;

namespace PSCLUITools
{
    [Cmdlet(VerbsCommon.New,"Menu2")]
    //[OutputType(typeof(FavoriteStuff))]
    public class NewMenu : PSCmdlet
    {
        [Parameter(
            Mandatory = false,
            Position = 0,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true)]
        public List<Object> InputObject { get; set; }
        /*
        [Parameter(
            Mandatory = true,
            Position = 0,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true)]
        public int FavoriteNumber { get; set; }

        [Parameter(
            Position = 1,
            ValueFromPipelineByPropertyName = true)]
        [ValidateSet("Cat", "Dog", "Horse")]
        public string FavoritePet { get; set; } = "Dog";
        */
        // This method gets called once for each cmdlet in the pipeline when the pipeline starts executing
        protected override void BeginProcessing()
        {
            // TODO Nothing I suppose
        }

        // This method will be called for each input received from the pipeline to this cmdlet; if no input is received, this method is not called
        protected override void ProcessRecord()
        {
            // TODO Collect InputObjects from the pipeline and feed them to the menu class in EndProcessing
        }

        // This method will be called once at the end of pipeline execution; if no input is received, this method is not called
        protected override void EndProcessing()
        {
            var buffer = new ConsoleBuffer();
            var container = new Container(10, 10, 40, 40);
            //container.SetContainerToWidestControlWidth = false;
            //container.SetControlsToContainerWidth = false;
            buffer.AddControl(container);

            var label = new Label(0, 0, "Listing");
            label.AddBorder("all");
            label.RemoveBorder("bottom");
            var menu = new Menu(0, 0, InputObject);
            menu.SetWidth(40);
            menu.AddBorder("all");

            //menu.TopDisplayedObjectIndex = 1;
            //menu.SelectedObjects.Add(InputObject[0]);

            container.AddControl(label);
            container.AddControl(menu);

            buffer.UpdateAll();
            buffer.Write();
            
            WriteObject(menu.ReadKey());
        }
    }

    /*
    public class FavoriteStuff
    {
        public int FavoriteNumber { get; set; }
        public string FavoritePet { get; set; }
    }
    */
}
