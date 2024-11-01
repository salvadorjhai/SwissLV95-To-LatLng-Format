Imports System.IO
Imports System.Net
Imports System.Text
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq

Module Module1

    Sub Main()

        Dim jo = JRaw.Parse(File.ReadAllText(".\input.json", New UTF8Encoding(False))).SelectToken("features")

        For i = 0 To jo.Count - 1
            Console.Title = $"SwissLV95 To LatLng Format | {i + 1} of {jo.Count}"

            Dim e_lv95 As Decimal = jo.ElementAt(i).SelectToken("geometry.coordinates[0]").Value(Of Decimal)
            Dim n_lv95 As Decimal = jo.ElementAt(i).SelectToken("geometry.coordinates[1]").Value(Of Decimal)
            Dim coordinates = LV95ToWGS84(e_lv95, n_lv95)

            WriteLine($"LV95: {e_lv95}, {n_lv95}")
            WriteLine($"Coordinates: {coordinates.Item1} , {coordinates.Item2}")
            WriteLine($"Reverse Geo: {ReverseGeocode(coordinates.Item1, coordinates.Item2)}")
            WriteLine("")

        Next

    End Sub

    Sub WriteLine(v As String)
        Console.WriteLine(v)
        File.AppendAllLines(".\log.txt", {v}, New UTF8Encoding(False))
    End Sub

    Function LV95ToWGS84(e_lv95 As Decimal, n_lv95 As Decimal) As (Decimal, Decimal)
        ' Step 1: Convert LV95 to LV03
        Dim e_lv03 As Decimal = e_lv95 - 2000000
        Dim n_lv03 As Decimal = n_lv95 - 1000000

        ' Step 2: Calculate auxiliary values
        Dim y As Decimal = (e_lv03 - 600000) / 1000000
        Dim x As Decimal = (n_lv03 - 200000) / 1000000

        ' Step 3: Calculate WGS84 latitude and longitude
        Dim lat As Decimal = 16.902389199999998 + 3.238272 * x - 0.270978 * Math.Pow(y, 2) - 0.002528 * Math.Pow(x, 2) - 0.0447 * Math.Pow(y, 2) * x - 0.014 * Math.Pow(x, 3)
        Dim lon As Decimal = 2.6779094 + 4.728982 * y + 0.791484 * y * x + 0.1306 * y * Math.Pow(x, 2) - 0.0436 * Math.Pow(y, 3)

        ' Step 4: Convert to degrees
        Dim latitude As Decimal = lat * (100 / 36)
        Dim longitude As Decimal = lon * (100 / 36)

        ' Return as tuple (latitude, longitude)
        Return (latitude, longitude)
    End Function


    Dim reverseGeoDic As Dictionary(Of String, JToken) = Nothing

    Function ReverseGeocode(latitude As Decimal, longitude As Decimal) As String
        Dim fl = ".\openstreemap.js"

        If IsNothing(reverseGeoDic) Then
            If File.Exists(fl) Then
                reverseGeoDic = JsonConvert.DeserializeObject(Of Dictionary(Of String, JToken))(File.ReadAllText(fl, New UTF8Encoding(False)))
            End If
        End If
        If IsNothing(reverseGeoDic) Then reverseGeoDic = New Dictionary(Of String, JToken)


        'with zoomlevel
        'https://nominatim.openstreetmap.org/ui/reverse.html?lat=47.355674918598467&lon=8.5544194450408586&zoom=17
        'https://nominatim.openstreetmap.org/reverse.php?lat=47.355675&lon=8.554419&zoom=17&format=jsonv2

        Dim apiUrl As String = $"https://nominatim.openstreetmap.org/reverse?lat={Decimal.Round(Decimal.Parse(latitude), 6)}&lon={Decimal.Round(Decimal.Parse(longitude), 6)}&format=json"

        If reverseGeoDic.ContainsKey(apiUrl) Then
            Return reverseGeoDic(apiUrl)("display_name").ToString()
        End If

        Dim request As WebRequest = WebRequest.Create(apiUrl)
        request.Method = "GET"

        DirectCast(request, HttpWebRequest).UserAgent = "VB.NET Test"

        Try
            Using response As WebResponse = request.GetResponse()
                Using dataStream As Stream = response.GetResponseStream()
                    Using reader As New StreamReader(dataStream)
                        Dim jsonResponse As String = reader.ReadToEnd()
                        Dim js = JRaw.Parse(jsonResponse)
                        Dim locationData As Dictionary(Of String, Object) = JsonConvert.DeserializeObject(Of Dictionary(Of String, Object))(jsonResponse)

                        If reverseGeoDic.ContainsKey(apiUrl) = False Then
                            reverseGeoDic.Add(apiUrl, js)
                            File.WriteAllText(fl, JsonConvert.SerializeObject(reverseGeoDic), New UTF8Encoding(False))
                        End If

                        ' Extract display_name or other fields from the JSON response
                        If locationData.ContainsKey("display_name") Then
                            Return locationData("display_name").ToString()
                        Else
                            Return "Location not found."
                        End If

                    End Using
                End Using
            End Using
        Catch ex As Exception
            Return "Error: " & ex.Message
        Finally
            Task.Delay(1000).Wait()
        End Try
    End Function

End Module
