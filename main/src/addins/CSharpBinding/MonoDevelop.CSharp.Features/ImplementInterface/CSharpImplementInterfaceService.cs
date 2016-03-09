﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Composition;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Simplification;
using Roslyn.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ICSharpCode.NRefactory6.CSharp.Features.ImplementInterface
{
	class CSharpImplementInterfaceService : AbstractImplementInterfaceService
	{
		protected override bool TryInitializeState(
			Document document, SemanticModel model, SyntaxNode node, CancellationToken cancellationToken,
			out SyntaxNode classOrStructDecl, out INamedTypeSymbol classOrStructType, out IEnumerable<INamedTypeSymbol> interfaceTypes)
		{
			if (!cancellationToken.IsCancellationRequested)
			{
				var interfaceNode = node as TypeSyntax;
				if (interfaceNode != null && interfaceNode.Parent is BaseTypeSyntax &&
					interfaceNode.Parent.IsParentKind(SyntaxKind.BaseList) &&
					((BaseTypeSyntax)interfaceNode.Parent).Type == interfaceNode)
				{
					if (interfaceNode.Parent.Parent.IsParentKind(SyntaxKind.ClassDeclaration) ||
						interfaceNode.Parent.Parent.IsParentKind(SyntaxKind.StructDeclaration))
					{
						var interfaceSymbolInfo = model.GetSymbolInfo(interfaceNode, cancellationToken);
						if (interfaceSymbolInfo.CandidateReason != CandidateReason.WrongArity)
						{
							var interfaceType = interfaceSymbolInfo.GetAnySymbol() as INamedTypeSymbol;
							cancellationToken.ThrowIfCancellationRequested();

							if (interfaceType != null && interfaceType.TypeKind == TypeKind.Interface)
							{
								classOrStructDecl = interfaceNode.Parent.Parent.Parent as TypeDeclarationSyntax;
								classOrStructType = model.GetDeclaredSymbol(classOrStructDecl, cancellationToken) as INamedTypeSymbol;
								interfaceTypes = SpecializedCollections.SingletonEnumerable(interfaceType);

								return interfaceTypes != null && classOrStructType != null;
							}
						}
					}
				}
			}

			classOrStructDecl = null;
			classOrStructType = null;
			interfaceTypes = null;
			return false;
		}

		protected override bool CanImplementImplicitly
		{
			get
			{
				return true;
			}
		}

		protected override bool HasHiddenExplicitImplementation
		{
			get
			{
				return true;
			}
		}

		private static ClassDeclarationSyntax GetClassDeclarationAt(SyntaxNode root, int position)
		{
			var node = root.FindToken(position).Parent.FirstAncestorOrSelf((SyntaxNode n) => n.IsKind(SyntaxKind.ClassDeclaration));
			return node as ClassDeclarationSyntax;
		}

		protected override bool CanImplementDisposePattern(INamedTypeSymbol symbol, SyntaxNode classDecl)
		{
			// The dispose pattern is only applicable if the implementing type is a class that does not already declare any conflicting
			// members named 'disposedValue' or 'Dispose' (because we will be generating a 'disposedValue' field and a couple of methods
			// named 'Dispose' as part of implementing the dispose pattern).
			return (classDecl != null) &&
				classDecl.IsKind(SyntaxKind.ClassDeclaration) &&
				(symbol != null) &&
				!symbol.GetMembers().Any(m => (m.MetadataName == "Dispose") || (m.MetadataName == "disposedValue"));
		}

		protected override Document ImplementDisposePattern(Document document, SyntaxNode root, INamedTypeSymbol symbol, int position, bool explicitly)
		{
			var classDecl = GetClassDeclarationAt(root, position);
			Debug.Assert(CanImplementDisposePattern(symbol, classDecl), "ImplementDisposePattern called with bad inputs");

			// Generate the IDisposable boilerplate code.  The generated code cannot be one giant resource string
			// because of the need to parse, format, and simplify the result; during pseudo-localized builds, resource
			// strings are given a special prefix and suffix that will break the parser, hence the requirement to
			// localize the comments individually.
			var code = string.Format (@"
    #region IDisposable Support
    private bool disposedValue = false; // {0}

    {1}void Dispose(bool disposing)
			{{
					if (!disposedValue)
					{{
							if (disposing)
							{{
									// {2}
								}}

							// {3}
							// {4}

							disposedValue = true;
						}}
				}}

				// {5}
				// ~{6}() {{
				//   // {7}
				//   Dispose(false);
				// }}

				// {8}
				{9}Dispose()
			{{
					// {10}
					Dispose(true);
					// {11}
					// GC.SuppressFinalize(this);
				}}
				#endregion
				",
				Resources.ToDetectRedundantCalls, 
				(symbol.IsSealed ? "" : "protected virtual "), 
				Resources.DisposeManagedStateTodo, 
				Resources.FreeUnmanagedResourcesTodo, 
				Resources.SetLargeFieldsToNullTodo, 
				Resources.OverrideAFinalizerTodo, 
				classDecl.Identifier.Value, 
				Resources.DoNotChangeThisCodeUseDispose, 
				Resources.ThisCodeAddedToCorrectlyImplementDisposable, 
				(explicitly ? "void System.IDisposable." : "public void "), 
				Resources.DoNotChangeThisCodeUseDispose,
				Resources.UncommentTheFollowingIfFinalizerOverriddenTodo
			);

				var decls = SyntaxFactory.ParseSyntaxTree(code)
				.GetRoot().DescendantNodes().OfType<MemberDeclarationSyntax>()
				.Select(decl => decl.WithAdditionalAnnotations(Formatter.Annotation, Simplifier.Annotation))
				.ToArray();

			// Append #endregion to the trailing trivia of the last declaration being generated.
			decls[decls.Length - 1] = decls[decls.Length - 1].WithAppendedTrailingTrivia(
				SyntaxFactory.TriviaList(
					SyntaxFactory.Trivia(SyntaxFactory.EndRegionDirectiveTrivia(true)),
					SyntaxFactory.CarriageReturnLineFeed));

			// Ensure that open and close brace tokens are generated in case they are missing.
			var newNode = classDecl.EnsureOpenAndCloseBraceTokens().AddMembers(decls);

			return document.WithSyntaxRoot(root.ReplaceNode(classDecl, newNode));
		}
	}
}
