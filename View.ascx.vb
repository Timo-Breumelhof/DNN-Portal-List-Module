Imports FortyFingers.DNN.Modules.PortalList.Components
Imports DotNetNuke
Imports DotNetNuke.Application
Imports DotNetNuke.Services.Exceptions

Imports DotNetNuke.Entities.Modules
Imports DotNetNuke.Entities.Portals
Imports DotNetNuke.Entities.Tabs
Imports DotNetNuke.Services.Localization


Imports DotNetNuke.UI.Skins

Imports System.IO
Imports System.Web.UI
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.Drawing.Text




Public Class View

    Inherits PortalModuleBase

    Dim objConfig As Config


    Protected Sub Page_Load(ByVal sender As Object, ByVal e As System.EventArgs) Handles Me.Load


        objConfig = New Config(Settings, ModuleId, TabModuleId)
        litOutput.Text = GetPortals()


    End Sub


#Region "Functions and Subs"


    ' Builds the HTML output for all visible portals, grouped by category when category filters are configured.
    Private Function GetPortals() As String

        Dim sPortals As String = ""
        Dim Portals As ArrayList

        ' Default to 5000 as a practical upper bound; DNN's GetPortalsByName requires a page size
        Dim iMaxPortals As Integer = 5000
        If objConfig.MaxPortals > 0 Then iMaxPortals = objConfig.MaxPortals

        Portals = PortalController.GetPortalsByName("%", 0, iMaxPortals, iMaxPortals)

        Dim oPortal As DotNetNuke.Entities.Portals.PortalInfo

        Dim oTabC As New DotNetNuke.Entities.Tabs.TabController
        Dim oTab As DotNetNuke.Entities.Tabs.TabInfo

        Dim oSkinC As New UI.Skins.SkinController

        Dim i As Integer

        Dim oCatFilters As New CategoryFilters(objConfig.CategoryFilter)

        ' "All" bucket collects every portal regardless of category; per-category buckets are used when categories are configured
        Dim dictPortals As New Dictionary(Of String, String)
        dictPortals.Add("All", "")

        For Each filter As CategoryFilter In oCatFilters.Filters

            dictPortals.Add(filter.Name, "")

        Next



        'Sort Direction the portals on portal id
        'Create arraylist
        Dim alSortPortals As New ArrayList

        'Get all portals and add them to the arraylist
        For i = 0 To Portals.Count - 1
            oPortal = Portals(i)

            If IsAllowedPortal(oPortal) Then 'Exclude some portals
                Dim SortPortal As New SortPortal

                Dim portCategory As String = oCatFilters.GetCategory(oPortal.PortalName)

                SortPortal.Add(oPortal.PortalName, oPortal.PortalID, portCategory, i)
                alSortPortals.Add(SortPortal)
            End If

        Next

        If alSortPortals.Count > 1 Then

            SortPortal.SortType = objConfig.SortType

            'Sort the arraylist
            alSortPortals.Sort()

            ' CompareTo is implemented descending, so ASC requires an extra Reverse()
            If objConfig.SortDirection = SortOrderType.ASC Then
                alSortPortals.Reverse()
            End If

        End If


        Dim tempSortPortal As SortPortal
        Dim iOrigIndex As Integer

        'Make sure the max portals is not higher then the actual number of portals
        If iMaxPortals > alSortPortals.Count Or iMaxPortals = 0 Then iMaxPortals = alSortPortals.Count

        Dim iStart As Integer = 0
        Dim iEnd As Integer = iMaxPortals - 1


        ' Loop over Portals

        For i = iStart To iEnd

            Dim homePageAccessibleToAllUsers As Boolean = False

            'Get the Sortportals original index
            tempSortPortal = alSortPortals(i)
            iOrigIndex = tempSortPortal.OriginalIndex

            oPortal = Portals(iOrigIndex)
            Dim sPortalTemplate As String = objConfig.ItemTemplate
            Dim sSkinSrc As String = String.Empty


            ' Use the portal's own default language, not the host portal's thread culture,
            ' to avoid matching stale DB entries for deleted locales with the wrong skin.
            sSkinSrc = PortalController.GetPortalSetting("DefaultPortalSkin", oPortal.PortalID, "", oPortal.DefaultLanguage)
            If sSkinSrc = "" Then
                ' Fallback: try each active locale (covers portals whose default language has no entry)
                For Each kvp As KeyValuePair(Of String, Locale) In LocaleController.Instance.GetLocales(oPortal.PortalID)
                    sSkinSrc = PortalController.GetPortalSetting("DefaultPortalSkin", oPortal.PortalID, "", kvp.Key)
                    If sSkinSrc <> "" Then Exit For
                Next
            End If

            Dim SkinSrcFrom As String = "Portal Theme:" & oPortal.PortalID.ToString()

            'Get First page skin
            If oPortal.HomeTabId > 0 Then

                Try
                    oTab = oTabC.GetTab(oPortal.HomeTabId, oPortal.PortalID, True)


                    'Check if All users has right to view the home page..
                    Dim oTPC As DotNetNuke.Security.Permissions.TabPermissionCollection = oTab.TabPermissions

                    For Each oTP As DotNetNuke.Security.Permissions.TabPermissionInfo In oTPC
                        If String.Equals(oTP.RoleName, "All Users", StringComparison.InvariantCultureIgnoreCase) AndAlso oTP.AllowAccess Then
                            homePageAccessibleToAllUsers = True
                            Exit For
                        End If
                    Next

                    If oTab.SkinSrc.Trim > "" Then
                        sSkinSrc = oTab.SkinSrc
                        SkinSrcFrom = "Tab Theme:" & oTab.TabID
                    End If

                Catch ex As Exception
                    '' The home page cannot be found
                End Try

            End If

            'What if there is no home page set? Add code to get the skin of the first page of the portal
            Dim sIconFilename As String = GetSkinImage(sSkinSrc, oPortal)



            If homePageAccessibleToAllUsers Or DotNetNuke.Security.Permissions.TabPermissionController.CanAddContentToPage Then


                Dim strPortalAlias = FormatPortalAliases(oPortal.PortalID)

                sPortalTemplate = sPortalTemplate.Replace("[Category]", tempSortPortal.Category)
                sPortalTemplate = sPortalTemplate.Replace("[PortalAlias]", strPortalAlias)
                sPortalTemplate = sPortalTemplate.Replace("[PortalName]", oPortal.PortalName)
                sPortalTemplate = sPortalTemplate.Replace("[PortalSkinImg]", sIconFilename)
                sPortalTemplate = sPortalTemplate.Replace("[PortalFooterText]", oPortal.FooterText)
                sPortalTemplate = sPortalTemplate.Replace("[PortalDescription]", oPortal.Description)
                sPortalTemplate = sPortalTemplate.Replace("[PortalId]", oPortal.PortalID)
                sPortalTemplate = sPortalTemplate.Replace("[SkinSrc]", sSkinSrc)
                sPortalTemplate = sPortalTemplate.Replace("[SkinSrcFrom]", SkinSrcFrom)



                dictPortals("All") &= sPortalTemplate
                dictPortals(tempSortPortal.Category) &= sPortalTemplate


            End If

        Next



        If dictPortals.Count > 1 Then

            For Each item As KeyValuePair(Of String, String) In dictPortals

                If Not item.Key = "All" Then

                    Dim sPortal As String = String.Empty


                    sPortal &= item.Value

                    sPortals &= objConfig.CategoryNameTemplate.Replace("[Category]", item.Key)
                    sPortals &= objConfig.RootTemplate.Replace("[Portals]", sPortal)

                End If


            Next
        Else
            sPortals = objConfig.RootTemplate.Replace("[Portals]", dictPortals("All"))
        End If

        If sPortals = "" Then
            Return "<div class=""NormalRed"">No Portals match your criteria</div>"
        Else

            Return sPortals

        End If


    End Function





    Public Function FormatPortalAliases(ByVal PortalId As Integer) As String

        Dim strTemp As String = String.Empty


        Dim strModuleAlias As String = PortalSettings.PortalAlias.HTTPAlias
        Dim str As New System.Text.StringBuilder

        'Will be used to get the portal alias. Will be False when there's a default alias, True when there is none. 
        'In that case we return the first alias, unless it's a child portal...
        Dim bFoundDefault As Boolean = False

        'Response.Write("* " & strModuleAlias & "<br>")

        Try
            Dim strFirstAlias As String = String.Empty
            Dim strReturnAlias As String = String.Empty

            Dim strDefaulAlias = GetDefaultAlias(PortalId)



            'Loop through all Portal Aliasses as even if a default is set sometimes it does not exist any more.
            'If no default PortAlias is found, get the first one from all aliasses
            'Unless the portal is a child portal, in that case compare the aliasses with the portal alias the module is on.



            Dim arr = PortalAliasController.Instance.GetPortalAliasesByPortalId(PortalId)
            Dim objPortalAliasInfo As PortalAliasInfo
            Dim i As Integer
            For i = 0 To arr.Count - 1

                'Get one of the Portal Aliases for this Site
                objPortalAliasInfo = CType(arr(i), PortalAliasInfo)


                'Response.Write("# " & objPortalAliasInfo.HTTPAlias & "<br>")

                'Store the first Alias (in case there's only one)
                If i = 0 Then
                    strFirstAlias = objPortalAliasInfo.HTTPAlias
                End If

                ' Child-portal alias (e.g. host/childsite) takes priority over the default alias
                ' because linking to the root domain would land on the wrong portal
                If objPortalAliasInfo.HTTPAlias.StartsWith(strModuleAlias & "/") Then
                    strReturnAlias = objPortalAliasInfo.HTTPAlias
                    Exit For
                End If

                'Test if this is the default Portal Alias


                If strDefaulAlias = objPortalAliasInfo.HTTPAlias Then
                    bFoundDefault = True
                    strReturnAlias = objPortalAliasInfo.HTTPAlias
                End If



            Next

            If strReturnAlias = String.Empty Then
                'This means the default alias was not found in the portalalias table
                strReturnAlias = strFirstAlias

            End If


            str.Append(String.Format("{0}://{1}", Request.Url.Scheme, strReturnAlias))


        Catch exc As Exception           'Module failed to load
            ProcessModuleLoadException(Me, exc)
        End Try

        ' Response.Write("*------<br>")

        Return str.ToString

    End Function



    Private Function GetDefaultAlias(PortalId As Integer) As String


        Return PortalController.GetPortalSetting("DefaultPortalAlias", PortalId, "")


    End Function




    Private Function GetSkinImage(ByVal sFileName As String, ByVal oPortal As DotNetNuke.Entities.Portals.PortalInfo) As String

        If sFileName = "" Then
            'Get the portal skin
            Dim portalSkinSource = SkinController.FormatSkinSrc(SkinController.GetDefaultPortalSkin(), PortalSettings)
            sFileName = portalSkinSource


        End If

        sFileName = sFileName.Replace("[G]", "/Portals/_Default/")
        sFileName = sFileName.Replace("[L]", "/" & oPortal.HomeDirectory & "/")

        Dim sFileJpg = sFileName.Replace(".ascx", ".jpg")

        If File.Exists(Server.MapPath("~" & sFileJpg)) Then
            Return (sFileJpg)
        End If

        Dim sFilePng = sFileName.Replace(".ascx", ".png")

        If File.Exists(Server.MapPath("~" & sFilePng)) Then
            Return (sFilePng)
        End If

        Return String.Empty



    End Function

    ''' <summary>
    ''' todo: check for 0 values for width and height
    ''' implement crop
    ''' </summary>
    ''' <param name="sFilePath">Path to the file relative to the BasePath</param>
    ''' <param name="sBasePath">BasePath</param>
    ''' <param name="iNewWidth">Width of new image</param>
    ''' <param name="iNewHeight">Height of new image</param>
    ''' <param name="FileNameAddition"></param>
    ''' <param name="Crop"></param>
    ''' <remarks></remarks>
    ''' 
    Private Function SaveImage(ByVal sFilePath As String, ByVal sBasePath As String, ByVal iNewWidth As Integer, ByVal iNewHeight As Integer, Optional ByVal FileNameAddition As String = "", Optional ByVal Crop As Boolean = False) As String

        Dim sCacheValue As String = String.Format("W={0},H={1},C={2}", iNewWidth, iNewHeight, Crop)

        Dim sServerBasePath As String = Server.MapPath("~" & sBasePath)

        If Not Directory.Exists(sServerBasePath) Then
            Directory.CreateDirectory(sServerBasePath)
        End If



        Dim sFileName As String = FileNameAddition & Path.GetFileNameWithoutExtension(sBasePath & "/" & sFilePath) & Path.GetExtension(sFilePath)
        Dim sNewClientPath As String = (sBasePath & "/" & sFileName).Replace("//", "/")

        'Check if the file already has been added to the cache
        If DotNetNuke.Common.Utilities.DataCache.GetCache(sNewClientPath) <> sCacheValue Or Not File.Exists(Server.MapPath("~" & sNewClientPath)) Then

            Dim oImage As New Bitmap(Server.MapPath(sFilePath))

            Dim iOrWidth = oImage.Width
            Dim iOrHeight = oImage.Height

            Dim oIcon As Bitmap

            Dim fMultiply As Double

            If objConfig.Crop Then
                Dim iRectX As Integer = 0
                Dim iRectY As Integer = 0
                Dim iRectWidth As Integer = iOrWidth
                Dim iRectHeight As Integer = iOrHeight


                If iNewWidth / iOrWidth > iNewHeight / iOrHeight Then
                    'Full width
                    Dim dCorrect As Double = 1 'used to correct if the original width < the target width
                    If iNewWidth > iOrWidth Then 'Only if the original > the target width
                        dCorrect = iOrWidth / iNewWidth
                    End If

                    fMultiply = (iNewHeight / iNewWidth) * dCorrect
                    iRectHeight = iRectWidth * fMultiply

                    If objConfig.CropCenterVertically And iNewHeight < iOrHeight Then
                        iRectY = (iOrHeight / 2) - (iRectHeight / 2) - 2
                        If iRectY < 0 Then iRectY = 0
                    End If

                Else
                    'Full height
                    Dim dCorrect As Double = 1 'used to correct if the target height < the image original height
                    If iNewHeight > iOrHeight Then
                        dCorrect = iOrHeight / iNewHeight
                    End If

                    fMultiply = (iNewWidth / iNewHeight) * dCorrect
                    iRectWidth = iRectHeight * fMultiply


                    If objConfig.CropCenterHorizontally And iNewWidth < iOrWidth Then
                        iRectX = (iOrWidth / 2) - (iRectWidth / 2) - 20
                        If iRectX < 0 Then iRectX = 0
                    End If

                End If

                Dim oImgRect As New Rectangle(iRectX, iRectY, iRectWidth, iRectHeight)
                oIcon = oImage.Clone(oImgRect, PixelFormat.Format24bppRgb)
                oIcon = oIcon.GetThumbnailImage(iNewWidth, iNewHeight, Nothing, Nothing)

            Else 'Crop the image

                If iOrWidth > iOrHeight Then 'Width is largest
                    fMultiply = iNewWidth / iOrWidth
                    iNewHeight = iOrHeight * fMultiply
                    iOrWidth = iNewWidth
                Else 'Height = largest
                    fMultiply = iNewHeight / iOrHeight
                    iNewWidth = iOrWidth * fMultiply
                    iOrHeight = iNewHeight
                End If

                oIcon = oImage.GetThumbnailImage(iNewWidth, iNewHeight, Nothing, Nothing)

            End If


            Dim JpegQuality As Long = 100

            Dim jgpEncoder As ImageCodecInfo = GetEncoder(ImageFormat.Jpeg)
            Dim myEncoder As System.Drawing.Imaging.Encoder = System.Drawing.Imaging.Encoder.Quality
            Dim myEncoderParameters As New EncoderParameters(1)
            Dim myEncoderParameter As New EncoderParameter(myEncoder, JpegQuality)
            myEncoderParameters.Param(0) = myEncoderParameter

            oIcon.Save(Path.Combine(sServerBasePath, sFileName), jgpEncoder, myEncoderParameters)
            oImage = Nothing
            oIcon = Nothing

            DotNetNuke.Common.Utilities.DataCache.SetCache(sNewClientPath, sCacheValue)

        End If

        Return (sNewClientPath)


    End Function


    Private Function GetEncoder(ByVal format As ImageFormat) As ImageCodecInfo

        Dim codecs As ImageCodecInfo() = ImageCodecInfo.GetImageDecoders()

        Dim codec As ImageCodecInfo
        For Each codec In codecs
            If codec.FormatID = format.Guid Then
                Return codec
            End If
        Next codec
        Return Nothing

    End Function


    Private Function IsAllowedPortal(ByVal oPortal As DotNetNuke.Entities.Portals.PortalInfo) As Boolean

        Dim passesFilters = True

        'Check if this portal should be included
        If objConfig.Filter Then

            ' If no filter criteria specified, don't filter at all
            If (objConfig.PortalIdFilter Is Nothing OrElse objConfig.PortalIdFilter.Trim = "") _
               AndAlso (objConfig.PortalAliasFilter Is Nothing OrElse objConfig.PortalAliasFilter.Trim = "") _
               AndAlso (objConfig.PortalDescriptionFilter Is Nothing OrElse objConfig.PortalDescriptionFilter.Trim = "") _
               AndAlso (objConfig.PortalKeywordFilter Is Nothing OrElse objConfig.PortalKeywordFilter.Trim = "") Then
                Return True
            End If

            'Check portal id
            For Each sPortalId As String In objConfig.PortalIdFilter.Split(",")
                If sPortalId.Trim = oPortal.PortalID.ToString Then
                    passesFilters = False
                End If
            Next

            'Check PortalAlias Regex
            Try
                If objConfig.PortalAliasFilter > "" And Not Regex.IsMatch(FormatPortalAliases(oPortal.PortalID), objConfig.PortalAliasFilter, RegexOptions.IgnoreCase) Then
                    passesFilters = False
                End If
            Catch ex As Exception
            End Try

            'Check Description Regex
            Try
                If objConfig.PortalDescriptionFilter > "" And Not Regex.IsMatch(oPortal.Description, objConfig.PortalDescriptionFilter, RegexOptions.IgnoreCase) Then
                    passesFilters = False
                End If
            Catch ex As Exception
            End Try

            'Check Keyword Regex
            Try
                If objConfig.PortalKeywordFilter > "" And Not Regex.IsMatch(oPortal.KeyWords, objConfig.PortalKeywordFilter, RegexOptions.IgnoreCase) Then
                    passesFilters = False
                End If
            Catch ex As Exception
            End Try
        Else
            'No filtering
            Return True
        End If

        ' XOR: passesFilters=True + FilterInclude=True → allow; passesFilters=True + FilterInclude=False (exclude mode) → deny
        Return (Not (passesFilters Xor objConfig.FilterInclude))

    End Function

#Region "Private Classes"


    Private Class SortPortal
        'Collection of Portals used to be able to Sort
        Implements IComparable
        Public PortalName As String
        Public PortalId As Integer
        Public Category As String
        Public OriginalIndex As Integer


        Public Sub Add(ByVal PortalName As String, ByVal PortalId As Integer, ByVal Category As String, ByVal OriginalIndex As Integer)
            Me.PortalName = PortalName
            Me.PortalId = PortalId
            Me.Category = Category
            Me.OriginalIndex = OriginalIndex
        End Sub


        ''' <summary>
        ''' Sort type
        ''' </summary>
        ''' <remarks></remarks>
        Private Shared _SortType As PortalOrder
        Public Shared Property SortType() As String
            Get
                Return _SortType
            End Get
            Set(ByVal value As String)
                _SortType = value
            End Set
        End Property




        Public Function CompareTo(ByVal obj As Object) As Integer _
           Implements System.IComparable.CompareTo
            If Not TypeOf obj Is SortPortal Then
                Throw New Exception("Object is not a Portal")
            End If

            Dim Compare As SortPortal = CType(obj, SortPortal)

            Select Case SortType

                Case PortalOrder.PortalId

                    Dim result As Integer = Compare.PortalId.CompareTo(Me.PortalId)

                    If result = 0 Then
                        result = Me.PortalId.CompareTo(Compare.PortalId)
                    End If
                    Return result

                Case PortalOrder.PortalName

                    Dim result As Integer = Compare.PortalName.CompareTo(Me.PortalName)

                    If result = 0 Then
                        result = Me.PortalName.CompareTo(Compare.PortalName)
                    End If
                    Return result


                Case PortalOrder.PortalAlias



            End Select



        End Function

    End Class

#End Region

#End Region

End Class