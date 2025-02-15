// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Markdig.Helpers;
using Markdig.Parsers;
using Markdig.Syntax;

namespace Microsoft.DotNet.Try.Markdown
{
    public class AnnotatedCodeBlockParser : FencedBlockParserBase<AnnotatedCodeBlock>
    {
        private readonly CodeFenceAnnotationsParser codeFenceAnnotationsParser;
        private int _order;

        public AnnotatedCodeBlockParser(CodeFenceAnnotationsParser codeFenceAnnotationsParser)
        {
            this.codeFenceAnnotationsParser = codeFenceAnnotationsParser ?? throw new ArgumentNullException(nameof(codeFenceAnnotationsParser));
            OpeningCharacters = new[] { '`' };
            InfoParser = ParseCodeOptions;
        }

        protected override AnnotatedCodeBlock CreateFencedBlock(BlockProcessor processor) =>
            new AnnotatedCodeBlock(this, _order++);

        protected bool ParseCodeOptions(BlockProcessor state, ref StringSlice line, IFencedBlock fenced, char openingCharacter)
        {
            if (!(fenced is AnnotatedCodeBlock codeLinkBlock))
            {
                return false;
            }

            var result = codeFenceAnnotationsParser.TryParseCodeFenceOptions(line.ToString(),
                state.Context);

            switch (result)
            {
                case NoCodeFenceOptions _:
                    return false;
                case FailedCodeFenceOptionParseResult failed:
                    foreach (var errorMessage in failed.ErrorMessages)
                    {
                        codeLinkBlock.Diagnostics.Add(errorMessage);
                    }

                    break;
                case SuccessfulCodeFenceOptionParseResult successful:
                    codeLinkBlock.Annotations = successful.Annotations;
                    break;
            }

            return true;
        }

        public override BlockState TryContinue(
            BlockProcessor processor,
            Block block)
        {
            var fence = (IFencedBlock) block;
            var count = fence.FencedCharCount;
            var matchChar = fence.FencedChar;
            var c = processor.CurrentChar;

            // Match if we have a closing fence
            var line = processor.Line;
            while (c == matchChar)
            {
                c = line.NextChar();
                count--;
            }

            // If we have a closing fence, close it and discard the current line
            // The line must contain only fence opening character followed only by whitespaces.
            if (count <= 0 && !processor.IsCodeIndent && (c == '\0' || c.IsWhitespace()) && line.TrimEnd())
            {
                block.UpdateSpanEnd(line.Start - 1);

                // Don't keep the last line
                return BlockState.BreakDiscard;
            }

            // Reset the indentation to the column before the indent
            processor.GoToColumn(processor.ColumnBeforeIndent);

            return BlockState.Continue;
        }
    }
}