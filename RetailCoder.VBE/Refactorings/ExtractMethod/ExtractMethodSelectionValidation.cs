﻿using System;
using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using Rubberduck.Common;
using Rubberduck.Parsing;
using Rubberduck.Parsing.Grammar;
using Rubberduck.Parsing.Symbols;
using Rubberduck.VBEditor;

namespace Rubberduck.Refactorings.ExtractMethod
{
    public class ExtractMethodSelectionValidation : IExtractMethodSelectionValidation
    {
        private IEnumerable<Declaration> _declarations;
        private List<Tuple<ParserRuleContext, string>> _invalidContexts = new List<Tuple<ParserRuleContext, string>>();

        public ExtractMethodSelectionValidation(IEnumerable<Declaration> declarations)
        {
            _declarations = declarations;
        }

        public IEnumerable<Tuple<ParserRuleContext, string>> InvalidContexts { get { return _invalidContexts; } }

        public bool ValidateSelection(QualifiedSelection qualifiedSelection)
        {
            var selection = qualifiedSelection.Selection;
            IEnumerable<Declaration> procedures = _declarations.Where(d => d.IsUserDefined && (DeclarationExtensions.ProcedureTypes.Contains(d.DeclarationType)));
            Func<int, dynamic> ProcOfLine = (sl) => procedures.FirstOrDefault(d => d.Context.Start.Line < sl && d.Context.Stop.EndLine() > sl);

            var startLine = selection.StartLine;
            var endLine = selection.EndLine;

            // End of line is easy
            var procEnd = ProcOfLine(endLine);
            if (procEnd == null)
            {
                return false;
            }

            var procEndContext = procEnd.Context as ParserRuleContext;
            var procEndLine = procEndContext.Stop.EndLine();

            /* Handle: function signature continuations
             * public function(byval a as string _
             *                 byval b as string) as integer
             */
            var procStart = ProcOfLine(startLine);
            if (procStart == null)
            {
                return false;
            }

            ParserRuleContext procStartContext;
            procStartContext = procStart.Context;
            VBAParser.EndOfStatementContext procEndOfSignature;

            switch (procStartContext)
            {
                case VBAParser.FunctionStmtContext funcStmt:
                    procEndOfSignature = funcStmt.endOfStatement();
                    break;
                case VBAParser.SubStmtContext subStmt:
                    procEndOfSignature = subStmt.endOfStatement();
                    break;
                case VBAParser.PropertyGetStmtContext getStmt:
                    procEndOfSignature = getStmt.endOfStatement();
                    break;
                case VBAParser.PropertyLetStmtContext letStmt:
                    procEndOfSignature = letStmt.endOfStatement();
                    break;
                case VBAParser.PropertySetStmtContext setStmt:
                    procEndOfSignature = setStmt.endOfStatement();
                    break;
                default:
                    return false;
            }

            var procSignatureLastLine = procEndOfSignature.Start.Line;

            if (!(((procEnd as Declaration).QualifiedSelection.Equals((procStart as Declaration).QualifiedSelection))
                && ((procEndOfSignature.Start.Line < selection.StartLine)
                || (procEndOfSignature.Start.Line == selection.StartLine && procEndOfSignature.Start.Column < selection.StartColumn))
                ))
                return false;

            /* At this point, we know the selection is within a single procedure. We need to validate that the user's
             * selection in fact contain only BlockStmt and not other stuff that might not be so extractable.
             */
            var visitor = new ExtractValidatorVisitor(qualifiedSelection, _invalidContexts);
            var results = visitor.Visit(procStartContext);
            _invalidContexts = visitor.InvalidContexts;

            if (_invalidContexts.Count() == 0)
            {
                // We've provved that there are no invalid statements contained in the selection. However, we need to analyze
                // the statements to ensure they are not partial selections.

                // The visitor will not return the results in a sorted manner, so we need to arrange the contexts in the same order.
                var sorted = results.OrderBy(context => context.Start.StartIndex);
                var finalResults = new List<VBAParser.BlockStmtContext>();
                ContextIsContainedOnce(sorted, ref finalResults, qualifiedSelection);
                return (results.Count() > 0 && _invalidContexts.Count() == 0);
            }
            return false;
        }

        /// <summary>
        /// The function ensure that we return only top-level BlockStmtContexts that
        /// exists within an user's selection, excluding any nested BlockStmtContexts
        /// which are also "selected" and thus ensure that we build an unique list 
        /// of BlockStmtContexts that corresponds to the user's selection. The function
        /// also will validate there are no overlapping selections which could be invalid.
        /// </summary>
        /// <param name="context">The context to test</param>
        /// <param name="aggregate">The list of contexts we already added to verify we are not adding one of its children or itself more than once</param>
        /// <param name="qualifiedSelection"></param>
        /// <returns>Boolean with true indicating that it's the first time we encountered a context in a user's selection and we can safely add it to the list</returns>
        private void ContextIsContainedOnce(IEnumerable<VBAParser.BlockStmtContext> sortedResults, ref List<VBAParser.BlockStmtContext> aggregate, QualifiedSelection qualifiedSelection)
        {
            foreach (var context in sortedResults)
            {
                if (qualifiedSelection.Selection.Contains(context))
                {
                    if (!aggregate.Any(otherContext => otherContext.GetSelection().Contains(context) && context != otherContext))
                    {
                        aggregate.Add(context);
                    }
                }
                else
                {
                    // We need to check if there was a partial selection made which would be invalid. It's OK if it's wholly contained inside
                    // a context (e.g. an inner If/End If block within a bigger If/End If was selected which is legal. However, selecting only
                    // part of inner If/End If block and a part of the outermost If/End If block should be illegal).
                    if (qualifiedSelection.Selection.Overlaps(context.GetSelection()) && !qualifiedSelection.Selection.IsContainedIn(context))
                    {
                        _invalidContexts.Add(Tuple.Create(context as ParserRuleContext, "Extract method must contain selection that represents a set of complete statements. It cannot extract a part of statement."));
                    }
                }
            }
        }

        private class ExtractValidatorVisitor : VBAParserBaseVisitor<IEnumerable<VBAParser.BlockStmtContext>>
        {
            private readonly QualifiedSelection _qualifiedSelection;
            private readonly List<Tuple<ParserRuleContext, string>> _invalidContexts;

            public ExtractValidatorVisitor(QualifiedSelection qualifiedSelection, List<Tuple<ParserRuleContext, string>> invalidContexts)
            {
                _qualifiedSelection = qualifiedSelection;
                _invalidContexts = invalidContexts;
            }

            public List<Tuple<ParserRuleContext, string>> InvalidContexts { get { return _invalidContexts; } }

            protected override IEnumerable<VBAParser.BlockStmtContext> DefaultResult => new List<VBAParser.BlockStmtContext>();

            public override IEnumerable<VBAParser.BlockStmtContext> VisitErrorStmt([NotNull] VBAParser.ErrorStmtContext context)
            {
                _invalidContexts.Add(Tuple.Create(context as ParserRuleContext, "Extract method cannot extract methods that contains an Error statement."));
                return null;
            }

            public override IEnumerable<VBAParser.BlockStmtContext> VisitEndStmt([NotNull] VBAParser.EndStmtContext context)
            {
                _invalidContexts.Add(Tuple.Create(context as ParserRuleContext, "Extract method cannot extract methods that contains an End statement."));
                return null;
            }

            public override IEnumerable<VBAParser.BlockStmtContext> VisitExitStmt([NotNull] VBAParser.ExitStmtContext context)
            {
                _invalidContexts.Add(Tuple.Create(context as ParserRuleContext, "Extract method cannot extract methods that contains an Exit statement"));
                return null;
            }

            public override IEnumerable<VBAParser.BlockStmtContext> VisitGoSubStmt([NotNull] VBAParser.GoSubStmtContext context)
            {
                _invalidContexts.Add(Tuple.Create(context as ParserRuleContext, "Extract method cannot extract methods that contains a GoSub statement"));
                return null;
            }

            public override IEnumerable<VBAParser.BlockStmtContext> VisitGoToStmt([NotNull] VBAParser.GoToStmtContext context)
            {
                _invalidContexts.Add(Tuple.Create(context as ParserRuleContext, "Extract method cannot extract methods that contains a GoTo statement"));
                return null;
            }

            public override IEnumerable<VBAParser.BlockStmtContext> VisitOnErrorStmt([NotNull] VBAParser.OnErrorStmtContext context)
            {
                _invalidContexts.Add(Tuple.Create(context as ParserRuleContext, "Extract method cannot extract methods that contains a On Error statement"));
                return null;
            }
            
            public override IEnumerable<VBAParser.BlockStmtContext> VisitIdentifierStatementLabel([NotNull] VBAParser.IdentifierStatementLabelContext context)
            {
                _invalidContexts.Add(Tuple.Create(context as ParserRuleContext, "Extract method cannot extract methods that contains a Line Label statement"));
                return null;
            }

            public override IEnumerable<VBAParser.BlockStmtContext> VisitCombinedLabels([NotNull] VBAParser.CombinedLabelsContext context)
            {
                _invalidContexts.Add(Tuple.Create(context as ParserRuleContext, "Extract method cannot extract methods that contains a Line Label statement"));
                return null;
            }

            public override IEnumerable<VBAParser.BlockStmtContext> VisitOnGoSubStmt([NotNull] VBAParser.OnGoSubStmtContext context)
            {
                _invalidContexts.Add(Tuple.Create(context as ParserRuleContext, "Extract method cannot extract methods that contains a On ... GoSub statement"));
                return null;
            }

            public override IEnumerable<VBAParser.BlockStmtContext> VisitOnGoToStmt([NotNull] VBAParser.OnGoToStmtContext context)
            {
                _invalidContexts.Add(Tuple.Create(context as ParserRuleContext, "Extract method cannot extract methods that contains a On ... GoTo statement"));
                return null;
            }

            public override IEnumerable<VBAParser.BlockStmtContext> VisitResumeStmt([NotNull] VBAParser.ResumeStmtContext context)
            {
                _invalidContexts.Add(Tuple.Create(context as ParserRuleContext, "Extract method cannot extract methods that contains a Resume statement"));
                return null;
            }

            public override IEnumerable<VBAParser.BlockStmtContext> VisitReturnStmt([NotNull] VBAParser.ReturnStmtContext context)
            {
                _invalidContexts.Add(Tuple.Create(context as ParserRuleContext, "Extract method cannot extract methods that contains a Return statement"));
                return null;
            }

            public override IEnumerable<VBAParser.BlockStmtContext> VisitBlockStmt([NotNull] VBAParser.BlockStmtContext context)
            {
                if (_invalidContexts.Count == 0)
                    //return base.VisitChildren(context);
                    return base.VisitBlockStmt(context).Concat(new List<VBAParser.BlockStmtContext> { context });
                else
                    return null;
            }

            protected override IEnumerable<VBAParser.BlockStmtContext> AggregateResult(IEnumerable<VBAParser.BlockStmtContext> aggregate, IEnumerable<VBAParser.BlockStmtContext> nextResult)
            {
                if (_invalidContexts.Count == 0)
                {
                    return aggregate.Concat(nextResult);
                }
                else
                    return null;
            }

            protected override bool ShouldVisitNextChild(IRuleNode node, IEnumerable<VBAParser.BlockStmtContext> currentResult)
            {
                // Don't visit any more children if we have any invalid contexts
                return (_invalidContexts.Count == 0);
            }
        }
    }
}
