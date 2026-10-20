'LGCMcn 第三方免费配色。独立预设数据，不更改官方赞助身份或生成解锁码。
Friend Module ModPersonalization
    Friend Structure PersonalizationPalette
        Public Hue As Integer
        Public Saturation As Integer
        Public LightAdjust As Integer
        Public Gradient As Integer
    End Structure

    Friend Function GetPersonalizationPalette(Id As Integer, Optional Hue As Integer = 180,
            Optional Saturation As Integer = 80, Optional Light As Integer = 20,
            Optional Gradient As Integer = 90) As PersonalizationPalette
        If Id = 14 Then
            Return New PersonalizationPalette With {
                .Hue = Math.Max(0, Math.Min(360, Hue)) Mod 360,
                .Saturation = Math.Max(0, Math.Min(100, Saturation)),
                .LightAdjust = Math.Max(0, Math.Min(40, Light)) - 20,
                .Gradient = Math.Max(0, Math.Min(180, Gradient)) - 90}
        End If
        Dim Hues = {210, 185, 135, 48, 28, 210, 330, 275, 40, 22, 5, 220, 185, 25}
        Dim Saturations = {85, 78, 62, 88, 48, 8, 72, 62, 82, 86, 75, 76, 78, 80}
        If Id < 0 OrElse Id >= Hues.Length Then Id = 0
        Return New PersonalizationPalette With {.Hue = Hues(Id), .Saturation = Saturations(Id),
            .LightAdjust = 0, .Gradient = If(Id = 12, 45, If(Id = 13, 35, 0))}
    End Function

    Friend Sub ApplyPersonalizationControls(Page As PageSetupUI)
        If Page Is Nothing Then Return
        For Each Item In Page.PanLauncherTheme.Children
            Dim Radio = TryCast(Item, MyRadioBox)
            If Radio Is Nothing OrElse Radio Is Page.RadioLauncherTheme5Gray Then Continue For
            Radio.IsEnabled = True
            Radio.Opacity = 1
            Radio.ToolTip = "LGCMcn 第三方免费配色，无需赞助码。"
        Next
        Page.RadioLauncherTheme5Gray.Visibility = Visibility.Collapsed
        For Each Overlay As FrameworkElement In {Page.LabLauncherTheme5Unlock, Page.LabLauncherTheme8Copy,
                Page.LabLauncherTheme9Copy, Page.LabLauncherTheme11Click, Page.PanLauncherHide}
            Overlay.Visibility = Visibility.Collapsed
            Overlay.IsHitTestVisible = False
        Next
    End Sub
End Module
