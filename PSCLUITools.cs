using System;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Management.Automation.Runspaces;
using System.Collections.Generic;

namespace PSCLUITools
{
    [Cmdlet(VerbsCommon.New,"Menu2")]
    //[OutputType(typeof(FavoriteStuff))]
    public class PSCLUITools : PSCmdlet
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
            var buffer = new ConsoleBuffer();

            var container = new Container(10, 5, 10, 10);
            //cont.SetContainerToWidestControlWidth = false;
            //outerContainer.AddControl(cont);
            buffer.AddControl(container);

            var lbl0 = new Label(0, 0, "qwertyuiopasdfghjklzxcvbnmqwertyuiop");
            container.AddControl(lbl0);
            //Console.WriteLine(cont.GetWidth());
            //Console.WriteLine(cont.GetHeight());
            lbl0.AddBorder("top");
            lbl0.AddBorder("right");
            lbl0.AddBorder("left");
            
            var menu = new Menu(0, 0, InputObject);
            container.AddControl(menu);
            menu.AddBorder("all");

            buffer.UpdateAll();
            buffer.Write();
        }

        // This method will be called for each input received from the pipeline to this cmdlet; if no input is received, this method is not called
        protected override void ProcessRecord()
        {
        }

        // This method will be called once at the end of pipeline execution; if no input is received, this method is not called
        protected override void EndProcessing()
        {
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
