﻿'    Mixer Calculation Routines 
'    Copyright 2008 Daniel Wagner O. de Medeiros
'
'    This file is part of DWSIM.
'
'    DWSIM is free software: you can redistribute it and/or modify
'    it under the terms of the GNU General Public License as published by
'    the Free Software Foundation, either version 3 of the License, or
'    (at your option) any later version.
'
'    DWSIM is distributed in the hope that it will be useful,
'    but WITHOUT ANY WARRANTY; without even the implied warranty of
'    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
'    GNU General Public License for more details.
'
'    You should have received a copy of the GNU General Public License
'    along with DWSIM.  If not, see <http://www.gnu.org/licenses/>.

Imports DWSIM.DrawingTools.GraphicObjects
Imports DWSIM.DWSIM.Flowsheet.FlowSheetSolver

Namespace UnitOperations

    <System.Serializable()> Public Class Mixer

        Inherits SharedClasses.UnitOperations.BaseClass

        Public Enum PressureBehavior
            Average
            Maximum
            Minimum
        End Enum

        Protected m_pressurebehavior As PressureBehavior = PressureBehavior.Minimum

        Public Property PressureCalculation() As PressureBehavior
            Get
                Return Me.m_pressurebehavior
            End Get
            Set(ByVal value As PressureBehavior)
                Me.m_pressurebehavior = value
            End Set
        End Property

        Public Sub New()
            MyBase.New()
        End Sub

        Public Sub New(ByVal name As String, ByVal description As String)

            MyBase.CreateNew()
            Me.ComponentName = name
            Me.ComponentDescription = description




        End Sub

        Public Overrides Function Calculate(Optional ByVal args As Object = Nothing) As Integer

            Dim form As Global.DWSIM.IFLowsheet = Me.Flowsheet
            Dim objargs As New DWSIM.Extras.StatusChangeEventArgs

            If Not Me.GraphicObject.OutputConnectors(0).IsAttached Then
                'Call function to calculate flowsheet
                With objargs
                    .Calculated = False
                    .Name = Me.Name
                    .ObjectType = ObjectType.NodeIn
                End With

                Throw New Exception(Me.FlowSheet.GetTranslatedString("Nohcorrentedematriac6"))
            End If

            Me.PropertyPackage.CurrentMaterialStream = Me.FlowSheet.SimulationObjects(Me.GraphicObject.OutputConnectors(0).AttachedConnector.AttachedTo.Name)

            Dim H, Hs, T, W, We, P As Double
            H = 0
            Hs = 0
            T = 0
            W = 0
            We = 0
            P = 0
            Dim i As Integer = 1
            Dim ms As DWSIM.SimulationObjects.Streams.MaterialStream
            Dim cp As ConnectionPoint
            For Each cp In Me.GraphicObject.InputConnectors
                If cp.IsAttached Then
                    If cp.AttachedConnector.AttachedFrom.Calculated = False Then Throw New Exception(Me.FlowSheet.GetTranslatedString("Umaoumaiscorrentesna"))
                    ms = Me.FlowSheet.SimulationObjects(cp.AttachedConnector.AttachedFrom.Name)
                    ms.Validate()
                    If Me.PressureCalculation = PressureBehavior.Minimum Then
                        If ms.Phases(0).Properties.pressure.GetValueOrDefault < P Then
                            P = ms.Phases(0).Properties.pressure
                        ElseIf P = 0 Then
                            P = ms.Phases(0).Properties.pressure
                        End If
                    ElseIf Me.PressureCalculation = PressureBehavior.Maximum Then
                        If ms.Phases(0).Properties.pressure.GetValueOrDefault > P Then
                            P = ms.Phases(0).Properties.pressure
                        ElseIf P = 0 Then
                            P = ms.Phases(0).Properties.pressure
                        End If
                    Else
                        P = P + ms.Phases(0).Properties.pressure.GetValueOrDefault
                        i += 1
                    End If

                    We = ms.Phases(0).Properties.massflow.GetValueOrDefault
                    W += We
                    If Not Double.IsNaN(ms.Phases(0).Properties.enthalpy.GetValueOrDefault) Then H += We * ms.Phases(0).Properties.enthalpy.GetValueOrDefault
                End If
            Next

            If W <> 0.0# Then Hs = H / W Else Hs = 0.0#

            If Me.PressureCalculation = PressureBehavior.Average Then P = P / (i - 1)

            T = 0

            Dim n As Integer = Me.FlowSheet.SimulationObjects(Me.GraphicObject.OutputConnectors(0).AttachedConnector.AttachedTo.Name).Phases(0).Compounds.Count
            Dim Vw As New Dictionary(Of String, Double)
            For Each cp In Me.GraphicObject.InputConnectors
                If cp.IsAttached Then
                    ms = Me.FlowSheet.SimulationObjects(cp.AttachedConnector.AttachedFrom.Name)
                    Dim comp As Interfaces.ICompound
                    For Each comp In ms.Phases(0).Compounds.Values
                        If Not Vw.ContainsKey(comp.Name) Then
                            Vw.Add(comp.Name, 0)
                        End If
                        Vw(comp.Name) += comp.MassFraction.GetValueOrDefault * ms.Phases(0).Properties.massflow.GetValueOrDefault
                    Next
                    If W <> 0.0# Then T += ms.Phases(0).Properties.massflow.GetValueOrDefault / W * ms.Phases(0).Properties.temperature.GetValueOrDefault
                End If
            Next

            If W = 0.0# Then T = 273.15

            Dim omstr As IMaterialStream = Me.FlowSheet.SimulationObjects(Me.GraphicObject.OutputConnectors(0).AttachedConnector.AttachedTo.Name)
            With omstr
                If W <> 0.0# Then .Phases(0).Properties.enthalpy = Hs
                .Phases(0).Properties.pressure = P
                .Phases(0).Properties.massflow = W
                .Phases(0).Properties.molarfraction = 1
                .Phases(0).Properties.massfraction = 1
                Dim comp As Interfaces.ICompound
                For Each comp In .Phases(0).Compounds.Values
                    If W <> 0.0# Then comp.MassFraction = Vw(comp.Name) / W
                Next
                Dim mass_div_mm As Double = 0
                Dim sub1 As Interfaces.ICompound
                For Each sub1 In .Phases(0).Compounds.Values
                    mass_div_mm += sub1.MassFraction.GetValueOrDefault / sub1.ConstantProperties.Molar_Weight
                Next
                For Each sub1 In .Phases(0).Compounds.Values
                    If W <> 0.0# Then
                        sub1.MoleFraction = sub1.MassFraction.GetValueOrDefault / sub1.ConstantProperties.Molar_Weight / mass_div_mm
                    Else
                        sub1.MoleFraction = 0.0#
                    End If
                Next
                Me.PropertyPackage.CurrentMaterialStream = Me.FlowSheet.SimulationObjects(Me.GraphicObject.OutputConnectors(0).AttachedConnector.AttachedTo.Name)
                If W <> 0.0# Then
                    Dim tmp = Me.PropertyPackage.DW_CalcEquilibrio_ISOL(PropertyPackages.FlashSpec.P, PropertyPackages.FlashSpec.H, P, Hs, T)
                    T = tmp(2)
                End If
                .Phases(0).Properties.temperature = T
                .SpecType = Interfaces.Enums.StreamSpec.Pressure_and_Enthalpy
            End With

            'Call function to calculate flowsheet
            With objargs
                .Calculated = True
                .Name = Me.Name
                .Tag = Me.GraphicObject.Tag
                .ObjectType = ObjectType.NodeIn
            End With

            form.CalculationQueue.Enqueue(objargs)

        End Function

        Public Overrides Function DeCalculate() As Integer

            Dim form As Global.DWSIM.IFLowsheet = Me.FlowSheet

            If Me.GraphicObject.OutputConnectors(0).IsAttached Then

                'Zerar valores da corrente de matéria conectada a jusante
                With Me.FlowSheet.SimulationObjects(Me.GraphicObject.OutputConnectors(0).AttachedConnector.AttachedTo.Name)
                    .Phases(0).Properties.temperature = Nothing
                    .Phases(0).Properties.pressure = Nothing
                    .Phases(0).Properties.molarfraction = 1
                    Dim comp As Interfaces.ICompound
                    For Each comp In .Phases(0).Compounds.Values
                        comp.MoleFraction = 0
                        comp.MassFraction = 0
                    Next
                    .Phases(0).Properties.massflow = Nothing
                    .Phases(0).Properties.molarflow = Nothing
                    .GraphicObject.Calculated = False
                End With

            End If

            'Call function to calculate flowsheet
            Dim objargs As New DWSIM.Extras.StatusChangeEventArgs
            With objargs
                .Calculated = False
                .Name = Me.Name
                .ObjectType = ObjectType.NodeIn
            End With



        End Function

        Public Overrides Function GetPropertyValue(ByVal prop As String, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As Object
            Return 0

        End Function

        Public Overloads Overrides Function GetProperties(ByVal proptype As Interfaces.Enums.PropertyType) As String()
            Dim i As Integer = 0
            Dim proplist As New ArrayList
            Return proplist.ToArray(GetType(System.String))
            proplist = Nothing
        End Function

        Public Overrides Function SetPropertyValue(ByVal prop As String, ByVal propval As Object, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As Boolean
            Return 0

        End Function

        Public Overrides Function GetPropertyUnit(ByVal prop As String, Optional ByVal su As Interfaces.IUnitsOfMeasure = Nothing) As String
            Return 0

        End Function
    End Class

End Namespace