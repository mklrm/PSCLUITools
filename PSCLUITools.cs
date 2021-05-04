﻿using System;
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
        public List<Object> PipelineInputList = new List<Object>();
        // This method gets called once for each cmdlet in the pipeline when the pipeline starts executing
        //protected override void BeginProcessing()
        //{
        //}

        protected override void ProcessRecord()
        {
            PipelineInputList.Add(InputObject[0]);
        }

        protected override void EndProcessing()
        {
            if (PipelineInputList.Count > 1)
                InputObject = PipelineInputList;

            //var buffer = new ConsoleBuffer();
            //var container = new Container(0, 0, Console.WindowWidth, Console.WindowHeight);
            //buffer.AddControl(container);

            //var label = new Label(0, 0, "This is a thing");
            //label.AddBorder("all");
            //label.AddPadding("all");
            //label.SetHeight(label.GetHeight() + 1);

            //container.AddControl(label);

            //var x = Console.WindowWidth / 2 - container.GetWidth() / 2;
            //container.SetHorizontalPosition(x);

            //var y = Console.WindowHeight / 2 - container.GetHeight() / 2;
            //container.SetVerticalPosition(y);

            var buffer = new ConsoleBuffer();
            var container = new Container(0, 0, Console.WindowWidth, Console.WindowHeight);
            buffer.AddControl(container);

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
