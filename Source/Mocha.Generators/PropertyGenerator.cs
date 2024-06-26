﻿using Microsoft.CodeAnalysis;
using System.CodeDom.Compiler;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Mocha.Generators
{
	[Generator( LanguageNames.CSharp )]
	public class PropertyGenerator : IIncrementalGenerator
	{
		private const string WithPropertyAttributeHint = "WithPropertyAttribute.g.cs";
		private const string OutputFileHint = "WithPropertyGen.g.cs";

		private const string WithPropertyAttribute = "Mocha.WithPropertyAttribute";

		public void Initialize( IncrementalGeneratorInitializationContext context )
		{
			context.RegisterPostInitializationOutput( PostInitialize );

			var provider = context.SyntaxProvider.ForAttributeWithMetadataName( WithPropertyAttribute, SyntaxPredicate, TransformWithPropertyAttribute )
				.Where( obj => obj != default );

			context.RegisterSourceOutput( provider.Collect(), Execute );
		}

		private void PostInitialize( IncrementalGeneratorPostInitializationContext context )
		{
			context.AddSource( WithPropertyAttributeHint, """
				namespace Mocha;

				/// <summary>
				/// <para>
				/// <b>Note:</b> this structure must be marked as partial.
				/// </para>
				/// </summary>
				[global::System.AttributeUsage( global::System.AttributeTargets.Field, AllowMultiple = false, Inherited = false )]
				public class WithPropertyAttribute : global::System.Attribute
				{
					public string? Name { get; }

					public WithPropertyAttribute()
					{

					}

					public WithPropertyAttribute( string name )
					{
						Name = name;
					}
				}
				""" );
		}

		private static void Execute( SourceProductionContext context, ImmutableArray<WithPropertyData> withPropertyData )
		{
			using var output = new StringWriter();
			using var writer = new IndentedTextWriter( output );

			writer.WriteLine( "// <auto-generated/>" );
			writer.WriteLine();

			var groups = withPropertyData.GroupBy( entry => entry.ContainerFQN );

			foreach ( var group in groups )
			{
				var firstEntry = group.First();

				var containingNamespace = firstEntry.ContainingNamespace;
				var containerName = firstEntry.ContainerName;

				// Start group namespace
				if ( !string.IsNullOrWhiteSpace( containingNamespace ) )
				{
					writer.WriteLine( $"namespace {containingNamespace}" );
					writer.WriteLine( "{" );
					writer.Indent++;
				}

				// Start group containing type(s)
				writer.WriteLine( $"partial class {containerName}" );
				writer.WriteLine( "{" );
				writer.Indent++;

				foreach ( var entry in group )
				{
					var propertyName = entry.RequestedPropertyName;
					if ( propertyName is null )
						propertyName = GetPropertyName( entry.TargetName );
					else
						propertyName = propertyName.Substring( 1, propertyName.Length - 2 );

					writer.WriteLine( "/// <summary>" );
					writer.WriteLine( "/// <para>" );
					writer.WriteLine( $"/// <b>(Auto-generated)</b> from <see cref=\"{entry.TargetName}\" />" );
					writer.WriteLine( "/// </para>" );
					writer.WriteLine( "/// </summary>" );
					writer.WriteLine( $"public {entry.TypeFQN} {propertyName} => {entry.TargetName};" );
				}

				writer.Indent--;
				writer.WriteLine( "}" );
				// End group containing type(s)

				if ( !string.IsNullOrWhiteSpace( containingNamespace ) )
				{
					writer.Indent--;
					writer.WriteLine( "}" );
				}
				// End group namespace
			}

			context.CancellationToken.ThrowIfCancellationRequested();
			context.AddSource( OutputFileHint, output.ToString() );
		}

		// Will always be true because of the AttributeUsage constraint.
		private static bool SyntaxPredicate( SyntaxNode node, CancellationToken token ) => true;
		private static WithPropertyData TransformWithPropertyAttribute( GeneratorAttributeSyntaxContext context, CancellationToken token )
		{
			if ( context.TargetSymbol is not IFieldSymbol symbol )
				return default;

			var containerFQN = symbol.ContainingType.ToDisplayString( SymbolDisplayFormat.FullyQualifiedFormat );
			var targetName = symbol.Name;
			var typeFQN = symbol.Type.ToDisplayString( SymbolDisplayFormat.FullyQualifiedFormat );
			// FIXME: Support nested types
			var containerName = symbol.ContainingType.Name;
			var containingNamespace = symbol.ContainingNamespace.IsGlobalNamespace
				? null
				: symbol.ContainingNamespace.ToDisplayString();

			var attribute = context.Attributes.First();
			// NOTE: What is this used for?
			var requestedPropertyName = attribute.ConstructorArguments.FirstOrDefault().Value as string;

			return new WithPropertyData( containerFQN, targetName, requestedPropertyName, typeFQN, containerName,
				containingNamespace, [] );
		}

		private readonly record struct WithPropertyData
		{
			public readonly string ContainerFQN;
			public readonly string TargetName;
			public readonly string? RequestedPropertyName;
			public readonly string TypeFQN;
			public readonly string ContainerName;
			public readonly string? ContainingNamespace;

			public readonly bool IsError;
			public readonly ImmutableArray<Diagnostic> Diagnostics;

			public WithPropertyData( string containerFQN, string targetName, string? requestedPropertyName, string typeFQN,
				string containerName, string? containingNamespace, ImmutableArray<Diagnostic> diagnostics )
			{
				ContainerFQN = containerFQN;
				TargetName = targetName;
				RequestedPropertyName = requestedPropertyName;
				TypeFQN = typeFQN;
				ContainerName = containerName;
				ContainingNamespace = containingNamespace;

				IsError = false;
				Diagnostics = diagnostics;
			}

			public WithPropertyData( ImmutableArray<Diagnostic> diagnostics )
			{
				ContainerFQN = string.Empty;
				TargetName = string.Empty;
				RequestedPropertyName = null;
				TypeFQN = string.Empty;
				ContainerName = string.Empty;
				ContainingNamespace = null;

				IsError = true;
				Diagnostics = diagnostics;
			}
		}

		private static string GetPropertyName( string fieldName )
		{
			var sb = new StringBuilder();

			for ( int i = 0; i < fieldName.Length; i++ )
			{
				char c = fieldName[i];

				if ( c == '_' )
				{
					sb.Append( char.ToUpper( fieldName[i + 1] ) );
					i++;
				}
				else
				{
					sb.Append( c );
				}
			}

			return sb.ToString();
		}
	}
}
