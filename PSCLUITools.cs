using System;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Management.Automation.Runspaces;
using System.Collections.Generic;

namespace PSCLUITools
{
    [Cmdlet(VerbsCommon.New,"Menu2")]
    [OutputType(typeof(Object))]
    public class NewMenu : PSCmdlet
    {
        // Menu items
        [Parameter(
            Mandatory = false,
            Position = 0,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true)]
        public List<Object> InputObject { get; set; }

        // Align item names Center of Left
        [Parameter(Mandatory = false)]
        [ValidateSet("Center","Left")]
        public string AlignText { get; set; } = "Left";

        // The name of the property of items to be displayed on the menu, such as Name
        [Parameter(Mandatory = false)]
        public string DisplayProperty { get; set; }

        //     Default: Select one by hitting Enter
        // Multiselect: Pick multiple items with Space, select with Enter
        //        List: Display a list of items and return them
        [Parameter(Mandatory = false)]
        [ValidateSet("Multiselect","List","Default")]
        public string Mode { get; set; } = "Default";

        // A title / help text to display above the menu
        [Parameter(Mandatory = false)]
        public List<string> Title { get; set; } = new List<string>();

        // Align Title Center of Left
        [Parameter(Mandatory = false)]
        public SwitchParameter ListSelected { get; set; }

        // Horizontal position of the upper left corner
        [Parameter(Mandatory = false)]
        [ValidateRange(-1, int.MaxValue)]
        public int X { get; set; } = -1;

        // Vertical position of the upper left corner
        [Parameter(Mandatory = false)]
        [ValidateRange(-1, int.MaxValue)]
        public int Y { get; set; } = -1;

        // Width of the menu
        [Parameter(Mandatory = false)]
        [ValidateRange(-1, int.MaxValue)]
        public int Width { get; set; } = -1;

        // Height of the menu
        [Parameter(Mandatory = false)]
        [ValidateRange(-1, int.MaxValue)]
        public int Height { get; set; } = -1;

        // Character to write on empty cells like edges
        [Parameter(Mandatory = false)]
        public char Character { get; set; } = ' ';

        // Foreground color
        [Parameter(Mandatory = false)]
        public string ItemColor { get; set; }

        // Background color
        [Parameter(Mandatory = false)]
        public string BackgroundColor { get; set; }

        // Indicates current item
        [Parameter(Mandatory = false)]
        public string ItemHighlightColor { get; set; } = "Green";

        // Indicates an item is selected
        [Parameter(Mandatory = false)]
        public string ItemSelectedColor { get; set; } = "Yellow";

        // Indicates a color is both current and selected
        [Parameter(Mandatory = false)]
        public string ItemHighlightedAndSelectedColor { get; set; } = "Magenta";

        // Remove edge
        [Parameter(Mandatory = false)]
        public SwitchParameter NoEdge { get; set; }

        public List<Object> PipelineInputList = new List<Object>();

        protected override void ProcessRecord()
        {
            PipelineInputList.Add(InputObject[0]);
        }

        protected override void EndProcessing()
        {
            if (PipelineInputList.Count > 1)
                InputObject = PipelineInputList;

            //var buffer = new Buffer();
            var buffer = new Buffer(Host);
            var container = new Container(0, 0, Console.WindowWidth, Console.WindowHeight);
            buffer.Add(container);

            var label = new Label(0, 0, "Listing");
            label.AddBorder("all");
            label.RemoveBorder("bottom");
            label.AddPadding("left");
            label.AddPadding("right");
            var menu = new Menu(0, 0, InputObject);
            menu.AddBorder("all");
            menu.AddPadding("all");

            container.AddControl(label);
            container.AddControl(menu);

            var x = Console.WindowWidth / 2 - container.GetWidth() / 2;
            container.SetHorizontalPosition(x);

            var y = Console.WindowHeight / 2 - container.GetHeight() / 2;
            container.SetVerticalPosition(y);
            
            buffer.UpdateAll();
            buffer.Write();
            
            WriteObject(menu.ReadKey());
        }
    }
}
