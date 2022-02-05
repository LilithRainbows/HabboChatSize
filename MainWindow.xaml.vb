Imports System.IO
Imports Flazzy
Imports Flazzy.ABC
Imports Flazzy.ABC.AVM2.Instructions
Imports Flazzy.IO
Class MainWindow
    Public Sub New()
        InitializeComponent()
        Directory.SetCurrentDirectory(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location))
    End Sub

    Private Sub Button_Increase_Click(sender As Object, e As RoutedEventArgs) Handles Button_Increase.Click
        If Label_ChatPreview.FontSize < 40 Then
            Label_ChatPreview.FontSize += 1
            ShowFontSize()
        End If
    End Sub

    Private Sub Button_Decrease_Click(sender As Object, e As RoutedEventArgs) Handles Button_Decrease.Click
        If Label_ChatPreview.FontSize > 12 Then
            Label_ChatPreview.FontSize -= 1
            ShowFontSize()
        End If
    End Sub

    Sub ShowFontSize()
        Label_ChatPreview.Content = "Example: " & Label_ChatPreview.FontSize & " px |"
    End Sub

    Private Sub Button_Reset_Click(sender As Object, e As RoutedEventArgs) Handles Button_Reset.Click
        Label_ChatPreview.FontSize = 12
        ShowFontSize()
    End Sub
    Function GetClassByName(ByVal ABCFiles As List(Of ABCFile), ByVal RequestedClass As String) As ASClass
        For Each ABCfile In ABCFiles
            Try
                Return ABCfile.Classes.First(Function(x) x.QName.Name = RequestedClass)
            Catch
                'Class not found on current ABCFile
            End Try
        Next
        Throw New Exception("Class not found")
    End Function

    Function GetInstanceByName(ByVal ABCFiles As List(Of ABCFile), ByVal RequestedInstance As String) As ASInstance
        For Each ABCfile In ABCFiles
            Try
                Return ABCfile.Instances.First(Function(x) x.QName.Name = RequestedInstance)
            Catch
                'Instance not found on current ABCFile
            End Try
        Next
        Throw New Exception("Instance not found")
    End Function

    Private Sub Button_Save_Click(sender As Object, e As RoutedEventArgs) Handles Button_Save.Click
        Dim FlashFileOrigin As String = GetClientPath()
        Using Flash = New FlashFile(FlashFileOrigin)
            Flash.Disassemble()
            If IsFlashFileModded(Flash) Then
                Throw New Exception("HabboAir_Unmodified.swf is required!")
            End If
            Dim ChatClassesName = {"ChatBubble", "PooledChatBubble"}
            For Each ChatClassName In ChatClassesName
                Dim TempClass = GetInstanceByName(Flash.AbcFiles, ChatClassName)
                TempClass.GetABC().Pool.Integers(TempClass.GetConstant("MAX_HEIGHT").ValueIndex) = 1000
                For Each TempClassMethod In TempClass.GetMethods()
                    Dim TempClassMethodCode = TempClassMethod.Body.ParseCode()
                    For i As Integer = 0 To TempClassMethodCode.Count - 1
                        If TempClassMethodCode(i).OP = OPCode.PushByte Then
                            If CType(TempClassMethodCode(i), PushByteIns).Value = 108 Then
                                TempClassMethodCode.Insert(i, New GetLocal0Ins)
                                TempClassMethodCode(i + 1) = GetPropertyIns(TempClass.GetABC, "MAX_HEIGHT", ChatClassName)
                                i += 1
                            End If
                        End If
                    Next
                    TempClassMethod.Body.Code = TempClassMethodCode.ToArray()
                Next
                If ChatClassName = "PooledChatBubble" Then
                    Dim TempClassStyleMethod = TempClass.GetMethod("style")
                    Dim TempClassStyleMethodCode = TempClassStyleMethod.Body.ParseCode()
                    TempClassStyleMethodCode.InsertRange(TempClassStyleMethodCode.Count - 1, {New GetLocal0Ins, GetPropertyIns(TempClass.GetABC, "_style", ChatClassName), GetPropertyIns(TempClass.GetABC, "textFormat", ""), New PushByteIns(Label_ChatPreview.FontSize), SetPropertyIns(TempClass.GetABC, "size", "")})
                    TempClassStyleMethod.Body.Code = TempClassStyleMethodCode.ToArray()
                Else
                    Dim TempClassStyleMethod = TempClass.Constructor
                    Dim TempClassStyleMethodCode = TempClassStyleMethod.Body.ParseCode()
                    For i As Integer = 0 To TempClassStyleMethodCode.Count - 1
                        If TempClassStyleMethodCode(i).OP = OPCode.GetLocal Then
                            If CType(TempClassStyleMethodCode(i), GetLocalIns).Register = 5 Then
                                TempClassStyleMethodCode.InsertRange(i - 1, {New GetLocal0Ins, GetPropertyIns(TempClass.GetABC, "_style", ChatClassName), GetPropertyIns(TempClass.GetABC, "textFormat", ""), New PushByteIns(Label_ChatPreview.FontSize), SetPropertyIns(TempClass.GetABC, "size", "")})
                                TempClassStyleMethod.Body.Code = TempClassStyleMethodCode.ToArray()
                                Exit For
                            End If
                        End If
                    Next
                End If
            Next
            MarkFlashFileAsModded(Flash)
            Dim destination = FlashFileOrigin.Replace("HabboAir_Unmodified.swf", "HabboAir.swf")
            Using fileStream = File.Open(destination, FileMode.Create)
                Using fileWriter = New FlashWriter(fileStream)
                    Flash.Assemble(fileWriter, CompressionKind.ZLIB)
                End Using
            End Using
        End Using
        MsgBox("Ready! Restart the client to see the changes.")
    End Sub

    Function GetClientPath() As String
        Dim AppDataPath As String = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) & "\Habbo Launcher\downloads\air"
        AppDataPath += "\" & Directory.GetDirectories(AppDataPath).Max(Function(d) New DirectoryInfo(d).Name)
        Dim PossibleClientPaths = {Directory.GetCurrentDirectory(), AppDataPath}
        For Each PossibleClientPath In PossibleClientPaths
            If File.Exists(PossibleClientPath & "\HabboAir.swf") Then
                If File.Exists(PossibleClientPath & "\HabboAir_Unmodified.swf") Then
                    If IsFlashFileModded_FromDisk(PossibleClientPath & "\HabboAir.swf") = False Then
                        File.Delete(PossibleClientPath & "\HabboAir_Unmodified.swf")
                        File.Copy(PossibleClientPath & "\HabboAir.swf", PossibleClientPath & "\HabboAir_Unmodified.swf")
                    End If
                Else
                    File.Copy(PossibleClientPath & "\HabboAir.swf", PossibleClientPath & "\HabboAir_Unmodified.swf")
                End If
                Return PossibleClientPath & "\HabboAir_Unmodified.swf"
            End If
        Next
        Throw New Exception("Client not found")
    End Function

    Function GetPropertyIns(MyAbc As ABCFile, PropertyName As String, ClassName As String) As GetPropertyIns
        For Each MultiNameIndex In MyAbc.Pool.GetMultinameIndices(PropertyName)
            Dim TempPropertyIns As New GetPropertyIns(MyAbc, MultiNameIndex)
            If TempPropertyIns.PropertyName.Namespace.Name = ClassName Or TempPropertyIns.PropertyName.Namespace.Name.EndsWith(":" & ClassName) Then
                Return TempPropertyIns
            End If
        Next
        Throw New Exception("Requested property (" & PropertyName & " at " & ClassName & ") not found")
    End Function

    Function SetPropertyIns(MyAbc As ABCFile, PropertyName As String, ClassName As String) As SetPropertyIns
        For Each MultiNameIndex In MyAbc.Pool.GetMultinameIndices(PropertyName)
            Dim TempPropertyIns As New SetPropertyIns(MyAbc, MultiNameIndex)
            If TempPropertyIns.PropertyName.Namespace.Name = ClassName Or TempPropertyIns.PropertyName.Namespace.Name.EndsWith(":" & ClassName) Then
                Return TempPropertyIns
            End If
        Next
        Throw New Exception("Requested property (" & PropertyName & " at " & ClassName & ") not found")
    End Function

    Function IsFlashFileModded_FromDisk(FlashFile As String) As Boolean
        Using Flash = New FlashFile(FlashFile)
            Flash.Disassemble()
            Return IsFlashFileModded(Flash)
        End Using
    End Function

    Function IsFlashFileModded(Flash As FlashFile) As Boolean
        For Each FlashFileTag In Flash.Tags
            If FlashFileTag.Kind = Tags.TagKind.Metadata Then
                Dim metaDataTag = CType(FlashFileTag, Tags.MetadataTag)
                Return metaDataTag.Metadata.Contains("Modified Client")
            End If
        Next
        Return False
    End Function

    Sub MarkFlashFileAsModded(Flash As FlashFile)
        For Each FlashFileTag In Flash.Tags
            If FlashFileTag.Kind = Tags.TagKind.Metadata Then
                Dim metaDataTag = CType(FlashFileTag, Tags.MetadataTag)
                metaDataTag.Metadata += Environment.NewLine & Environment.NewLine & "<!--- Modified Client --->"
                Exit For
            End If
        Next
    End Sub

End Class
