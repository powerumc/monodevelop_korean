//
// EventListenerWrapper.cs
//
// Author:
//       Lluis Sanchez Gual <lluis@xamarin.com>
//
// Copyright (c) 2016 Xamarin, Inc (http://www.xamarin.com)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Reflection;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Text;

using MonoDevelop.Core.Execution;
using NUnit.Engine;
using NUnit.Common;
using System.Diagnostics.Contracts;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using System.Xml;
using System.Net.Configuration;
using System.Globalization;
using MonoDevelop.UnitTesting.NUnit;

namespace NUnit3Runner
{
	
	class EventListenerWrapper: MarshalByRefObject, ITestEventListener
	{
		RemoteProcessServer server;

		public EventListenerWrapper (RemoteProcessServer server)
		{
			this.server = server;
		}
		
		public void RunFinished (Exception exception)
		{
		}
		
		public void RunFinished (TestResult results)
		{
		}
		
		public void RunStarted (string name, int testCount)
		{
		}
		
		public void SuiteFinished (XmlNode testResult)
		{
			var testName = testResult.Attributes["fullname"].Value;
			server.SendMessage (new SuiteFinishedMessage {
				Suite = testName,
				Result = GetLocalTestResult (testResult)
			});
		}

		public void SuiteStarted (XmlNode testResult)
		{
			var testName = testResult.Attributes["fullname"].Value;
			server.SendMessage (new SuiteStartedMessage {
				Suite = testName
			});
		}
		
		public void TestFinished (XmlNode testResult)
		{
			var testName = testResult.Attributes["fullname"].Value;
			server.SendMessage (new TestFinishedMessage {
				TestCase = testName,
				Result = GetLocalTestResult (testResult)
			});
		}
		
		public void TestStarted (XmlNode data)
		{
			var testName = data.Attributes["fullname"].Value;
			server.SendMessage (new TestStartedMessage {
				TestCase = testName
			});
		}
		
		public override object InitializeLifetimeService ()
		{
			return null;
		}
		
		string GetTestName (XmlNode data)
		{
			return data.Attributes["fullname"].Value;
		}
		
		public RemoteTestResult GetLocalTestResult (XmlNode t)
		{
			var e = (XmlElement)t;

			RemoteTestResult res = new RemoteTestResult ();

			if (e.LocalName == "test-suite") {
				res.Failures = int.Parse (e.GetAttribute ("failed"));
				res.Errors = 0;
				res.Ignored = int.Parse (e.GetAttribute ("skipped"));
				res.Inconclusive = int.Parse (e.GetAttribute ("inconclusive"));
				res.NotRunnable = 0;
				res.Passed = int.Parse (e.GetAttribute ("passed"));
			} else if (e.LocalName == "test-case") {
				var runResult = e.GetAttribute ("result");
				if (runResult == "Passed")
					res.Passed = 1;
				else if (runResult == "Failed") {
					res.Failures = 1;
					var msg = e.SelectSingleNode ("failure/message");
					if (msg != null)
						res.Message = msg.InnerText;
					var stack = e.SelectSingleNode ("failure/stack-trace");
					if (stack != null)
						res.StackTrace = stack.InnerText;
				}
				else if (runResult == "Skipped") {
					res.Skipped = 1;
					var msg = e.SelectSingleNode ("reason/message");
					if (msg != null)
						res.Message = msg.InnerText;
				} else if (runResult == "Inconclusive") {
					res.Inconclusive = 1;
					var msg = e.SelectSingleNode ("reason/message");
					if (msg != null)
						res.Message = msg.InnerText;
				}
			}

			res.Time = TimeSpan.FromSeconds (double.Parse (e.GetAttribute ("duration"), CultureInfo.InvariantCulture));

			var output = e.SelectSingleNode ("output");
			if (output != null) {
				Console.WriteLine (output.InnerText);
				res.ConsoleOutput = output.InnerText;
			}
			
			return res;
		}

		public void UnhandledException (Exception exception)
		{
		}

		void ITestEventListener.OnTestEvent (string report)
		{
			var doc = new XmlDocument();
			doc.LoadXml (report);

			var testEvent = doc.FirstChild;
			switch (testEvent.Name)
			{
				case "test-case":
				TestFinished (testEvent);
				break;

				case "test-suite":
				SuiteFinished (testEvent);
				break;
			}
		}
	}
	
}
