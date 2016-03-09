//
// ExternalTestRunner.cs
//
// Author:
//   Lluis Sanchez Gual
//
// Copyright (C) 2005 Novell, Inc (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//


using System;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Engine;
using System.Xml;
using NUnit.Common;
using NUnit.Engine.Internal;
using MonoDevelop.Core.Execution;
using MonoDevelop.UnitTesting.NUnit;

namespace NUnit3Runner
{
	public class NUnitTestRunner: MessageListener
	{
		RemoteProcessServer server;
		ITestEngine engine;
		ITestFilterService filterService;

		public NUnitTestRunner (RemoteProcessServer server)
		{
			this.server = server;

			// Note: We need to load all nunit.*.dll assemblies before we do *anything* else in this class
			// This is to ensure that we always load the assemblies from the monodevelop directory and not
			// from the directory of the assembly under test. For example we wnat to load
			// /Applications/MonoDevelop/lib/Addins/nunit.framework.dll and not /user/app/foo/bin/debug/nunit.framework.dll
			var path = Path.GetDirectoryName (GetType ().Assembly.Location);
			string nunitPath = Path.Combine (path, "nunit.framework.dll");
			Assembly.LoadFrom (nunitPath);

			Initialize ();
		}

		public void Initialize ()
		{
			engine = TestEngineActivator.CreateInstance ();
			filterService = engine.Services.GetService<ITestFilterService>();
		}

		[MessageHandler]
		public RunResponse Run (RunRequest r)
		{
			EventListenerWrapper listenerWrapper = new EventListenerWrapper (server);

			UnhandledExceptionEventHandler exceptionHandler = (object sender, UnhandledExceptionEventArgs e) => {
				var ex = e.ExceptionObject;
				File.WriteAllText (r.CrashLogFile, e.ToString ());
			};

			AppDomain.CurrentDomain.UnhandledException += exceptionHandler;
			try {
				var res = Run (listenerWrapper, r.NameFilter, r.Path, r.SuiteName, r.SupportAssemblies, r.TestRunnerType, r.TestRunnerAssembly);
				res = res.SelectSingleNode ("test-suite");
				return new RunResponse () { Result = listenerWrapper.GetLocalTestResult (res) };
			} finally {
				AppDomain.CurrentDomain.UnhandledException -= exceptionHandler;
			}
		}

		[MessageHandler]
		public GetTestInfoResponse GetTestInfo (GetTestInfoRequest req)
		{
			var r = GetTestInfo (req.Path, req.SupportAssemblies);
			return new GetTestInfoResponse { Result = r };
		}

		TestPackage CreatePackage (string path)
		{
			TestPackage package = new TestPackage (path);
			package.AddSetting ("ShadowCopyFiles", false);
			package.AddSetting ("ProcessModel", "InProcess");
			package.AddSetting ("DomainUsage", "Single");
			return package;
		}
		
		public XmlNode Run (ITestEventListener listener, string[] nameFilter, string path, string suiteName, string[] supportAssemblies, string testRunnerType, string testRunnerAssembly)
		{
			InitSupportAssemblies (supportAssemblies);

			TestFilter filter = TestFilter.Empty;
			if (nameFilter != null && nameFilter.Length > 0)
				filter = CreateTestFilter (nameFilter);

			ITestRunner tr = null;
			if (!string.IsNullOrEmpty (testRunnerType)) {
				Type runnerType;
				if (string.IsNullOrEmpty (testRunnerAssembly))
					runnerType = Type.GetType (testRunnerType, true);
				else {
					var asm = Assembly.LoadFrom (testRunnerAssembly);
					runnerType = asm.GetType (testRunnerType);
				}
				tr = (ITestRunner)Activator.CreateInstance (runnerType);
			}

			TestPackage package = CreatePackage (path);

			if (tr == null)
				tr = engine.GetRunner (package);

			return tr.Run (listener, filter);
		}
		
		public NunitTestInfo GetTestInfo (string path, string[] supportAssemblies)
		{
			InitSupportAssemblies (supportAssemblies);
			TestPackage package = CreatePackage (path);
			var tr = engine.GetRunner (package);
			var r = tr.Explore (TestFilter.Empty);
			var root = r.SelectSingleNode ("test-suite") as XmlElement;
			if (root != null)
				return BuildTestInfo (root);
			else
				return null;
		}
		
		internal NunitTestInfo BuildTestInfo (XmlElement test)
		{
			NunitTestInfo ti = new NunitTestInfo ();
			// The name of inherited tests include the base class name as prefix.
			// That prefix has to be removed
			string tname = test.GetAttribute ("name");
			string fullName = test.GetAttribute ("fullname");

			var tn = test.GetAttribute ("classname");
			if (tn != null) {
				var i = tn.LastIndexOf ('.');
				if (i != -1) {
					ti.FixtureTypeName = tn.Substring (i + 1);
					ti.FixtureTypeNamespace = tn.Substring (0, i);
				} else {
					ti.FixtureTypeName = tn;
					ti.FixtureTypeNamespace = "";
				}
			}
			ti.Name = tname;
			ti.TestId = fullName;
			// Trim short name from end of full name to get the path
			string testNameWithDelimiter = "." + tname;
			if (fullName.EndsWith (testNameWithDelimiter)) {
				int pathLength = fullName.Length - testNameWithDelimiter.Length;
				ti.PathName = fullName.Substring (0, pathLength);
			}
			else
				ti.PathName = null;

			ti.IsExplicit = test.GetAttribute ("classname") == "Explicit";

			var children = test.ChildNodes.OfType<XmlElement> ().Where (e => e.LocalName == "test-suite" || e.LocalName == "test-case").ToArray ();
			if (children.Length > 0) {
				ti.Tests = new NunitTestInfo [children.Length];
				for (int n=0; n<children.Length; n++)
					ti.Tests [n] = BuildTestInfo (children [n]);
			}
			return ti;
		}
		
		void InitSupportAssemblies (string[] supportAssemblies)
		{
			// Preload support assemblies (they may not be in the test assembly directory nor in the gac)
			foreach (string asm in supportAssemblies)
				Assembly.LoadFrom (asm);
		}
		
		private TestFilter CreateTestFilter (string[] nameFilter)
		{
			ITestFilterBuilder builder = filterService.GetTestFilterBuilder();
			foreach (var testName in nameFilter)
				builder.AddTest(testName);
			return builder.GetFilter();
		}
	}
}

