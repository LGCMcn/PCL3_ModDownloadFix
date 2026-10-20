'Windows 版本遵循上游指南：固定蓝色主色调，不提供赞助主题的类似表现。
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
        Return New PersonalizationPalette With {.Hue = 210, .Saturation = 85, .LightAdjust = 0, .Gradient = 0}
    End Function
    Friend Sub ApplyPersonalizationControls(Page As PageSetupUI)
        If Page Is Nothing Then Return
        Page.PanLauncherTheme.Visibility = Visibility.Collapsed
        For Each Item In Page.PanLauncherTheme.Children
            Dim Radio = TryCast(Item, MyRadioBox)
            If Radio IsNot Nothing Then Radio.IsEnabled = False
        Next
        For Each Item As FrameworkElement In {Page.LabLauncherHue, Page.SliderLauncherHue, Page.LabLauncherSat, Page.SliderLauncherSat,
                Page.LabLauncherLight, Page.SliderLauncherLight, Page.LabLauncherDelta, Page.SliderLauncherDelta,
                Page.RadioLauncherTheme5Gray, Page.LabLauncherTheme5Unlock, Page.LabLauncherTheme8Copy,
                Page.LabLauncherTheme9Copy, Page.LabLauncherTheme11Click, Page.PanLauncherHide}
            Item.Visibility = Visibility.Collapsed
            Item.IsHitTestVisible = False
        Next
    End Sub
End Module
