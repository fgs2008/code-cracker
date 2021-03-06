﻿Imports CodeCracker.Design
Imports CodeCracker.Test.TestHelper
Imports Xunit

Namespace Design
    Public Class NameOfTests
        Inherits CodeFixTest(Of NameOfAnalyzer, NameOfCodeFixProvider)

        <Fact>
        Public Async Function IgnoreIfStringLiteralIsWhitespace() As Task
            Const test = "
Public Class TypeName
    Sub Foo()
        Dim whatever = """"
    End Sub
End Class"

            Await VerifyBasicHasNoDiagnosticsAsync(test)
        End Function

        <Fact>
        Public Async Function IgnoreIfStringLiteralIsNothing() As Task
            Const test = "
Public Class TypeName
    Sub Foo()
        Dim whatever = Nothing
    End Sub
End Class"

            Await VerifyBasicHasNoDiagnosticsAsync(test)
        End Function

        <Fact>
        Public Async Function IgnoreIfConstructorHasNoParameters() As Task
            Const test = "
Public Class TypeName
    Public Sub New()
        dim whatever = ""b""
    End Sub
End Class"

            Await VerifyBasicHasNoDiagnosticsAsync(test)
        End Function

        <Fact>
        Public Async Function IgnoreIfMethodHasNoParameters() As Task
            Const test = "
Public Class TypeName
    Sub Foo()
        Dim whatever = """"
    End Sub
End Class"

            Await VerifyBasicHasNoDiagnosticsAsync(test)
        End Function

        <Fact>
        Public Async Function IgnoreIfMethodHasParametersUnlineOfStringLiteral() As Task
            Const test = "
Public Class TypeName
    Sub Foo(a As String)
        Dim whatever = ""b""
    End Sub
End Class"

            Await VerifyBasicHasNoDiagnosticsAsync(test)
        End Function

        <Fact>
        Public Async Function WhenUsingStringLiteralEqualsParameterNameReturnAnalyzerCreatesDiagnostic() As Task
            Const test = "
Public Class TypeName
    Sub Foo(b As String)
        Dim whatever = ""b""
    End Sub
End Class"

            Dim expected = New DiagnosticResult With {
                .Id = NameOfAnalyzer.DiagnosticId,
                .Message = "Use 'NameOf(b)' instead of specifying the parameter name.",
                .Severity = Microsoft.CodeAnalysis.DiagnosticSeverity.Warning,
                .Locations = {New DiagnosticResultLocation("Test0.vb", 4, 24)}
            }

            Await VerifyDiagnosticsAsync(test, expected)
        End Function

        <Fact>
        Public Async Function WhenUsingStringLiteralEqualsParameterNameInConstructorFixItToNameof() As Task
            Const test = "
Public Class TypeName
    Sub New(b As String)
        Dim whatever = ""b""
    End Sub
End Class"

            Const fixtest = "
Public Class TypeName
    Sub New(b As String)
        Dim whatever = NameOf(b)
    End Sub
End Class"

            Await VerifyBasicFixAsync(test, fixtest, 0)
        End Function

        <Fact>
        Public Async Function WhenUsingStringLiteralEqualsParameterNameInConstructorFixItToNameofMustKeepComments() As Task
            Const test = "
Public Class TypeName
    Sub New(b As String)
        'a
        Dim whatever = ""b"" 'd
        'b
    End Sub
End Class"

            Const fixtest = "
Public Class TypeName
    Sub New(b As String)
        'a
        Dim whatever = NameOf(b) 'd
        'b
    End Sub
End Class"

            Await VerifyBasicFixAsync(test, fixtest, 0)
        End Function

        <Fact>
        Public Async Function WhenUsingStringLiteralEqualsParameterNameInMethodFixItToNameof() As Task
            Const test = "
Public Class TypeName
    Sub Foo(b As String)
        Dim whatever = ""b""
    End Sub
End Class"

            Const fixtest = "
Public Class TypeName
    Sub Foo(b As String)
        Dim whatever = NameOf(b)
    End Sub
End Class"

            Await VerifyBasicFixAsync(test, fixtest, 0)
        End Function

        <Fact>
        Public Async Function WhenUsingStringLiteralEqualsParameterNameInMethodMustKeepComments() As Task
            Const test = "
Public Class TypeName
    Sub Foo(b As String)
        'a
        Dim whatever = ""b""'d
        'b
    End Sub
End Class"

            Const fixtest = "
Public Class TypeName
    Sub Foo(b As String)
        'a
        Dim whatever = NameOf(b) 'd
        'b
    End Sub
End Class"

            Await VerifyBasicFixAsync(test, fixtest, 0)
        End Function
    End Class
End Namespace