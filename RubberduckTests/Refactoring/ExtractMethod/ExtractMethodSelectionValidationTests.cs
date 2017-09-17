using Microsoft.VisualStudio.TestTools.UnitTesting;
using Rubberduck.Refactorings.ExtractMethod;
using Rubberduck.VBEditor;
using RubberduckTests.Mocks;

namespace RubberduckTests.Refactoring.ExtractMethod
{
    [TestClass]
    public class ExtractMethodSelectionValidationTests
    {
        [TestMethod]
        [TestCategory("ExtractMethodSelectionValidationTests")]
        public void SpansSingleMethod_ShouldReturnFalse()
        {
            var inputCode = @"
Option Explicit
Private Sub Foo()
    Dim x As Integer
    x = 1 + 2
End Sub


Private Function NewMethod
    dim a as string
    Debug.Print a
End Function


Private Sub NewMethod4
    dim a as string

    Debug.Print a
End Sub";

            QualifiedModuleName qualifiedModuleName;
            var state = MockParser.ParseString(inputCode, out qualifiedModuleName);
            var declarations = state.AllDeclarations;
            var selection = new Selection(4, 4, 10, 14);
            QualifiedSelection? qSelection = new QualifiedSelection(qualifiedModuleName, selection);

            var SUT = new ExtractMethodSelectionValidation(declarations);

            var actual = SUT.ValidateSelection(qSelection.Value);
            Assert.IsFalse(actual);
        }

        [TestMethod]
        [TestCategory("ExtractMethodSelectionValidationTests")]
        public void SpansSingleMethod_ShouldReturnTrue()
        {
            var inputCode = @"
Option Explicit
Private Sub Foo()
    Dim x As Integer
    x = 1 + 2
End Sub


Private Sub NewMethod
    dim a as string
    Debug.Print a
End Sub


Private Sub NewMethod4
    dim a as string

    Debug.Print a
End Sub";

            QualifiedModuleName qualifiedModuleName;
            var state = MockParser.ParseString(inputCode, out qualifiedModuleName);
            var declarations = state.AllDeclarations;
            var selection = new Selection(4, 4, 5, 14);
            QualifiedSelection? qSelection = new QualifiedSelection(qualifiedModuleName, selection);

            var SUT = new ExtractMethodSelectionValidation(declarations);

            var actual = SUT.ValidateSelection(qSelection.Value);
            Assert.IsTrue(actual); 
        }

        [TestMethod]
        [TestCategory("ExtractMethodSelectionValidationTests")]
        public void SpansSingleMethod_LineContinuation_ShouldReturnFalse()
        {
            var inputCode = @"
Option Explicit
Private Sub Foo(byval a as long, _
                byval b as long)

    Dim x As Integer
    x = 1 + 2
End Sub


Private Sub NewMethod
End Sub";


            QualifiedModuleName qualifiedModuleName;
            var state = MockParser.ParseString(inputCode, out qualifiedModuleName);
            var declarations = state.AllDeclarations;
            var selection = new Selection(4, 4, 7, 14);
            QualifiedSelection? qSelection = new QualifiedSelection(qualifiedModuleName, selection);

            var SUT = new ExtractMethodSelectionValidation(declarations);

            var actual = SUT.ValidateSelection(qSelection.Value);
            Assert.IsFalse(actual);
        }

        [TestMethod]
        [TestCategory("ExtractMethodSelectionValidationTests")]
        public void SpansSingleMethod_LineContinuation_ShouldReturnTrue()
        {
            var inputCode = @"
Option Explicit
Private Sub Foo(byval a as long, _
                byval b as long)

    Dim x As Integer
    x = 1 + 2
End Sub


Private Sub NewMethod
End Sub";


            QualifiedModuleName qualifiedModuleName;
            var state = MockParser.ParseString(inputCode, out qualifiedModuleName);
            var declarations = state.AllDeclarations;
            var selection = new Selection(4, 33, 7, 14);
            QualifiedSelection? qSelection = new QualifiedSelection(qualifiedModuleName, selection);

            var SUT = new ExtractMethodSelectionValidation(declarations);

            var actual = SUT.ValidateSelection(qSelection.Value);
            Assert.IsTrue(actual);
        }

        [TestMethod]
        [TestCategory("ExtractMethodSelectionValidationTests")]
        public void SpansSingleMethod_LineContinuation_TooSoon_ShouldReturnFalse()
        {
            var inputCode = @"
Option Explicit
Private Sub Foo(byval a as long, _
                byval b as long)

    Dim x As Integer
    x = 1 + 2
End Sub


Private Sub NewMethod
End Sub";


            QualifiedModuleName qualifiedModuleName;
            var state = MockParser.ParseString(inputCode, out qualifiedModuleName);
            var declarations = state.AllDeclarations;
            var selection = new Selection(4, 32, 7, 14);
            QualifiedSelection? qSelection = new QualifiedSelection(qualifiedModuleName, selection);

            var SUT = new ExtractMethodSelectionValidation(declarations);

            var actual = SUT.ValidateSelection(qSelection.Value);
            Assert.IsFalse(actual);
        }

        [TestMethod]
        [TestCategory("ExtractMethodSelectionValidationTests")]
        public void SelectCompleteBlocks_NotValid_ShouldReturnFalse()
        {
            var inputCode = @"
Option Explicit
Private Sub Foo(byval a as long, _
                byval b as long)

    Dim x As Integer
    
    If x = 0 Then
        x = x + 1
        If x = 1 Then
            Debug.Print x
        End If
        x = 0
    End If
End Sub";


            QualifiedModuleName qualifiedModuleName;
            var state = MockParser.ParseString(inputCode, out qualifiedModuleName);
            var declarations = state.AllDeclarations;
            var selection = new Selection(8, 1, 12, 15);
            QualifiedSelection? qSelection = new QualifiedSelection(qualifiedModuleName, selection);

            var SUT = new ExtractMethodSelectionValidation(declarations);

            var actual = SUT.ValidateSelection(qSelection.Value);
            Assert.IsFalse(actual);
        }

        [TestMethod]
        [TestCategory("ExtractMethodSelectionValidationTests")]
        public void SelectCompleteBlocks_OutermostBlock_ShouldReturnTrue()
        {
            var inputCode = @"
Option Explicit
Private Sub Foo(byval a as long, _
                byval b as long)

    Dim x As Integer
    
    If x = 0 Then
        x = x + 1
        If x = 1 Then
            Debug.Print x
        End If
        x = 0
    End If
End Sub";


            QualifiedModuleName qualifiedModuleName;
            var state = MockParser.ParseString(inputCode, out qualifiedModuleName);
            var declarations = state.AllDeclarations;
            var selection = new Selection(8, 1, 14, 11);
            QualifiedSelection? qSelection = new QualifiedSelection(qualifiedModuleName, selection);

            var SUT = new ExtractMethodSelectionValidation(declarations);

            var actual = SUT.ValidateSelection(qSelection.Value);
            Assert.IsTrue(actual);
        }

        [TestMethod]
        [TestCategory("ExtractMethodSelectionValidationTests")]
        public void SelectCompleteBlocks_InnermostBlock_ShouldReturnTrue()
        {
            var inputCode = @"
Option Explicit
Private Sub Foo(byval a as long, _
                byval b as long)

    Dim x As Integer
    
    If x = 0 Then
        x = x + 1
        If x = 1 Then
            Debug.Print x
        End If
        x = 0
    End If
End Sub";


            QualifiedModuleName qualifiedModuleName;
            var state = MockParser.ParseString(inputCode, out qualifiedModuleName);
            var declarations = state.AllDeclarations;
            var selection = new Selection(10, 1, 12, 15);
            QualifiedSelection? qSelection = new QualifiedSelection(qualifiedModuleName, selection);

            var SUT = new ExtractMethodSelectionValidation(declarations);

            var actual = SUT.ValidateSelection(qSelection.Value);
            Assert.IsTrue(actual);
        }

        [TestMethod]
        [TestCategory("ExtractMethodSelectionValidationTests")]
        public void SelectCompleteBlocks_MultipleBlocksWithinOneBlock_ShouldReturnTrue()
        {
            var inputCode = @"
Option Explicit
Private Sub Foo(byval a as long, _
                byval b as long)

    Dim x As Integer
    
    If x = 0 Then
        x = x + 1
        If x = 1 Then
            Debug.Print x
        End If
        x = 0
    End If
End Sub";


            QualifiedModuleName qualifiedModuleName;
            var state = MockParser.ParseString(inputCode, out qualifiedModuleName);
            var declarations = state.AllDeclarations;
            var selection = new Selection(9, 1, 13, 14);
            QualifiedSelection? qSelection = new QualifiedSelection(qualifiedModuleName, selection);

            var SUT = new ExtractMethodSelectionValidation(declarations);

            var actual = SUT.ValidateSelection(qSelection.Value);
            Assert.IsTrue(actual);
        }

        [TestMethod]
        [TestCategory("ExtractMethodSelectionValidationTests")]
        public void SelectCompleteBlocks_MultipleBlocksWithinOneBlock_StartsTooLate_ShouldReturnFalse()
        {
            var inputCode = @"
Option Explicit
Private Sub Foo(byval a as long, _
                byval b as long)

    Dim x As Integer
    
    If x = 0 Then
        x = x + 1
        If x = 1 Then
            Debug.Print x
        End If
        x = 0
    End If
End Sub";


            QualifiedModuleName qualifiedModuleName;
            var state = MockParser.ParseString(inputCode, out qualifiedModuleName);
            var declarations = state.AllDeclarations;
            var selection = new Selection(9, 10, 13, 14);
            QualifiedSelection? qSelection = new QualifiedSelection(qualifiedModuleName, selection);

            var SUT = new ExtractMethodSelectionValidation(declarations);

            var actual = SUT.ValidateSelection(qSelection.Value);
            Assert.IsFalse(actual);
        }

        [TestMethod]
        [TestCategory("ExtractMethodSelectionValidationTests")]
        public void SelectCompleteBlocks_MultipleBlocksWithinOneBlock_EndsTooSoon_ShouldReturnFalse()
        {
            var inputCode = @"
Option Explicit
Private Sub Foo(byval a as long, _
                byval b as long)

    Dim x As Integer
    
    If x = 0 Then
        x = x + 1
        If x = 1 Then
            Debug.Print x
        End If
        x = 0
    End If
End Sub";


            QualifiedModuleName qualifiedModuleName;
            var state = MockParser.ParseString(inputCode, out qualifiedModuleName);
            var declarations = state.AllDeclarations;
            var selection = new Selection(9, 9, 13, 13);
            QualifiedSelection? qSelection = new QualifiedSelection(qualifiedModuleName, selection);

            var SUT = new ExtractMethodSelectionValidation(declarations);

            var actual = SUT.ValidateSelection(qSelection.Value);
            Assert.IsFalse(actual);
        }

        [TestMethod]
        [TestCategory("ExtractMethodSelectionValidationTests")]
        public void SelectCompleteBlocks_MultipleBlocksWithinOneBlock_Tight_ShouldReturnTrue()
        {
            var inputCode = @"
Option Explicit
Private Sub Foo(byval a as long, _
                byval b as long)

    Dim x As Integer
    
    If x = 0 Then
        x = x + 1
        If x = 1 Then
            Debug.Print x
        End If
        x = 0
    End If
End Sub";


            QualifiedModuleName qualifiedModuleName;
            var state = MockParser.ParseString(inputCode, out qualifiedModuleName);
            var declarations = state.AllDeclarations;
            var selection = new Selection(9, 9, 13, 14);
            QualifiedSelection? qSelection = new QualifiedSelection(qualifiedModuleName, selection);

            var SUT = new ExtractMethodSelectionValidation(declarations);

            var actual = SUT.ValidateSelection(qSelection.Value);
            Assert.IsTrue(actual);
        }
    }
}
