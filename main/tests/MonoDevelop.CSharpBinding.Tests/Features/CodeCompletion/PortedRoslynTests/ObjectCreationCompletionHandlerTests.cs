﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using NUnit.Framework;
using ICSharpCode.NRefactory6.CSharp.Completion;

namespace ICSharpCode.NRefactory6.CSharp.CodeCompletion.Roslyn
{
	[TestFixture]
	class ObjectCreationCompletionProviderTests : CompletionTestBase
	{
		internal override CompletionContextHandler CreateContextHandler ()
		{
			return (CompletionContextHandler)Activator.CreateInstance(typeof(CompletionEngine).Assembly.GetType ("ICSharpCode.NRefactory6.CSharp.Completion.ObjectCreationContextHandler"));
		}

		[Test]
		public void InObjectCreation()
		{
			var markup = @"
class MyGeneric<T> { }

void foo()
{
   MyGeneric<string> foo = new $$
}";

			VerifyItemExists(markup, "MyGeneric<string>");
		}
//
//		[Fact, Trait(Traits.Feature, Traits.Features.Completion)]
//		public void NotInAnonymouTypeObjectCreation1()
//		{
//			var markup = @"
//class C
//{
//    void M()
//    {
//        var x = new[] { new { Foo = ""asdf"", Bar = 1 }, new $$
//    }
//}";
//
//			VerifyItemIsAbsent(markup, "<anonymous type: string Foo, int Bar>");
//		}

		[Test]
		public void NotVoid()
		{
			var markup = @"
class C
{
    void M()
    {
        var x = new $$
    }
}";

			VerifyItemIsAbsent(markup, "void");
		}

		[Test]
		public void InYieldReturn()
		{
			var markup =
				@"using System;
using System.Collections.Generic;

class Program
{
    IEnumerable<FieldAccessException> M()
    {
        yield return new $$
    }
}";
			VerifyItemExists(markup, "System.FieldAccessException");
		}

		[Test]
		public void InAsyncMethodReturnStatement()
		{
			var markup =
				@"using System;
using System.Threading.Tasks;

class Program
{
    async Task<FieldAccessException> M()
    {
        await Task.Delay(1);
        return new $$
    }
}";
			VerifyItemExists(markup, "System.FieldAccessException");
		}
//
//		[Fact, Trait(Traits.Feature, Traits.Features.Completion)]
//		public void IsCommitCharacterTest()
//		{
//			var validCharacters = new[]
//			{
//				' ', '(', '{', '['
//			};
//
//			var invalidCharacters = new[]
//			{
//				'x', ',', '#'
//			};
//
//			foreach (var ch in validCharacters)
//			{
//				Assert.True(CompletionProvider.IsCommitCharacter(null, ch, null), "Expected '" + ch + "' to be a commit character");
//			}
//
//			foreach (var ch in invalidCharacters)
//			{
//				Assert.False(CompletionProvider.IsCommitCharacter(null, ch, null), "Expected '" + ch + "' to NOT be a commit character");
//			}
//		}
//
//		[Fact, Trait(Traits.Feature, Traits.Features.Completion)]
//		public void IsTextualTriggerCharacterTest()
//		{
//			VerifyTextualTriggerCharacter("Abc$$ ", shouldTriggerWithTriggerOnLettersEnabled: true, shouldTriggerWithTriggerOnLettersDisabled: true);
//			VerifyTextualTriggerCharacter("Abc $$X", shouldTriggerWithTriggerOnLettersEnabled: true, shouldTriggerWithTriggerOnLettersDisabled: false);
//			VerifyTextualTriggerCharacter("Abc $$@", shouldTriggerWithTriggerOnLettersEnabled: false, shouldTriggerWithTriggerOnLettersDisabled: false);
//			VerifyTextualTriggerCharacter("Abc$$@", shouldTriggerWithTriggerOnLettersEnabled: false, shouldTriggerWithTriggerOnLettersDisabled: false);
//			VerifyTextualTriggerCharacter("Abc$$.", shouldTriggerWithTriggerOnLettersEnabled: false, shouldTriggerWithTriggerOnLettersDisabled: false);
//		}
//
//		[Fact, Trait(Traits.Feature, Traits.Features.Completion)]
//		public void SendEnterThroughToEditorTest()
//		{
//			VerifySendEnterThroughToEnter("Foo", "Foo", sendThroughEnterEnabled: false, expected: false);
//			VerifySendEnterThroughToEnter("Foo", "Foo", sendThroughEnterEnabled: true, expected: true);
//		}

		[Test]
		public void SuggestAlias()
		{
			var markup = @"
using D = System.Globalization.DigitShapes; 
class Program
{
    static void Main(string[] args)
    {
        D d=  new $$
    }
}";
			VerifyItemExists(markup, "D");
		}

		[Test]
		public void SuggestAlias2()
		{
			var markup = @"
namespace N
{
using D = System.Globalization.DigitShapes; 
class Program
{
    static void Main(string[] args)
    {
        D d=  new $$
    }
}
}

";
			VerifyItemExists(markup, "D");
		}
//
//		[WorkItem(1075275)]
//		[Fact, Trait(Traits.Feature, Traits.Features.Completion)]
//		public void CommitAlias()
//		{
//			var markup = @"
//using D = System.Globalization.DigitShapes; 
//class Program
//{
//    static void Main(string[] args)
//    {
//        D d=  new $$
//    }
//}";
//
//			var expected = @"
//using D = System.Globalization.DigitShapes; 
//class Program
//{
//    static void Main(string[] args)
//    {
//        D d=  new D
//    }
//}";
//			VerifyProviderCommit(markup, "D", expected, '(', "");
//		}
	}
}
