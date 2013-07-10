using System;
using System.Collections;
using System.IO;

using Gtk;

using MonoDevelop.Core;
 
using MonoDevelop.Ide.Gui;
using MonoDevelop.Ide.Gui.Pads;
using MonoDevelop.Projects;
using MonoDevelop.Components.Commands;

namespace MonoDevelop.VersionControl
{
	public abstract class BaseView : ViewContent
	{
		public BaseView (string name)
		{
			ContentName = name;
		}
		
		protected virtual void SaveAs (string fileName)
		{
		}

		public override void Save (string fileName)
		{
			SaveAs (fileName);
		}
		
		public override bool IsReadOnly {
			get { return true; }
		}

		public override bool IsFile {
			get {
				return false;
			}
		}

		public override string TabPageLabel {
			get {
				return ContentName;
			}
		}
	}
}
