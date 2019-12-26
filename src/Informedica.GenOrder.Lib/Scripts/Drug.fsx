﻿
#I __SOURCE_DIRECTORY__

#load "../../../.paket/load/netstandard2.1/main.group.fsx"
#load "../../../../GenSolver/src/Informedica.GenSolver.Lib/Utils.fs"
#load "../../../../GenSolver/src/Informedica.GenSolver.Lib/Variable.fs"
#load "../../../../GenSolver/src/Informedica.GenSolver.Lib/Equation.fs"
#load "../../../../GenSolver/src/Informedica.GenSolver.Lib/Solver.fs"
#load "../../../../GenSolver/src/Informedica.GenSolver.Lib/Dtos.fs"
#load "../../../../GenSolver/src/Informedica.GenSolver.Lib/Api.fs"

#load "../DateTime.fs"
#load "../WrappedString.fs"
#load "../List.fs"
#load "../ValueUnit.fs"
#load "../ValueRange.fs"
#load "../VariableUnit.fs"
#load "../Solver.fs"
#load "../Orderable.fs"
#load "../Prescription.fs"
#load "../Order.fs"

#time

open MathNet.Numerics

open Informedica.GenUnits.Lib
open Informedica.GenOrder.Lib

module Units = ValueUnit.Units


module Order =
    
    open Order
    open Informedica.GenUtils.Lib.BCL
    open Informedica.GenSolver.Lib

    module ValueRange = Variable.ValueRange
    module Frequency = VariableUnit.Frequency
    module Quantity = VariableUnit.Quantity
    module TotalAdjust = VariableUnit.TotalAdjust
    module Dose = VariableUnit.Dose
    module DoseAdjust = VariableUnit.DoseAdjust
    module RateAdjust = VariableUnit.RateAdjust
    module Name = WrappedString.Name
    module Props = Solver.Props

    let printPrescriptions sn (o : Order) =

        match o.Prescription with
        | Prescription.Discontinuous fr ->
            [
                let frs =
                    fr
                    |> Frequency.toValueUnitStringList None
                    |> Seq.zip (fr |> Frequency.toVarUnt 
                                   |> VariableUnit.getValues)

                for (v, f) in frs do

                    let o =
                        o
                        |> Order.solve 
                            (o.Orderable.Name |> Name.toString) 
                            Mapping.Freq Props.Vals [v]

                    for (s, q, ta) in o.Orderable.Components
                             |> List.collect (fun c ->
                                c.Items
                             )
                             |> List.map (fun item ->
                                let (q, _, _) =
                                    item.Dose
                                    |> Dose.get
                                let (_, ta, _) =
                                    item.DoseAdjust
                                    |> DoseAdjust.get
                                item.Name |> Name.toString, q, ta
                             ) do
                        
                        for dt, dq in 
                            q 
                            |> Quantity.toValueUnitStringList (Some 2)
                            |> Seq.zip (ta |> TotalAdjust.toValueUnitStringList (Some 2)) do

                    
                            let n = 
                                o.Orderable.Name
                                |> Name.toString

                            sprintf "%s %s %s = (%s)" n f dq dt

            ]
        | Prescription.Continuous ->
            [
                let on = o.Orderable.Name |> Name.toString
                let rts =
                    o.Orderable.Dose
                        |> Dose.toVarUnt
                    |> fun (_, _, vru) ->
                        vru
                        |> VariableUnit.toValueUnitStringList id None
                        |> Seq.zip (vru |> VariableUnit.getValues)

                for (rv, rt) in rts do
                    let o =
                        o
                        |> Order.solve on Mapping.OrderableDoseRate Props.Vals [ rv ]
                    
                    for c in o.Orderable.Components do
                        let cqs =
                            c.OrderableQuantity
                            |> Quantity.toVarUnt
                            |> VariableUnit.getValues

                        for cq in cqs do

                            let o =
                                o
                                |> Order.solve 
                                    (c.Name |> Name.toString) 
                                    Mapping.ComponentOrderableQty
                                    Props.Vals
                                    [ cq ]

                            for itm in (o.Orderable.Components
                                        |> List.collect (fun c -> c.Items)
                                        |> List.filter (fun i -> i.Name |> Name.toString = sn)) do

                                let oq =
                                    o.Orderable.OrderableQuantity
                                    |> Quantity.toValueUnitStringList None
                                    |> Seq.zip (o.Orderable.OrderableQuantity
                                                |> Quantity.toVarUnt
                                                |> VariableUnit.getValues)

                                let da = 
                                    itm.DoseAdjust
                                    |> DoseAdjust.get
                                    |> (fun (_, _, ra) ->
                                        ra
                                        |> RateAdjust.toValueUnitStringList (Some 2)
                                    )

                                let iq =
                                    itm.OrderableQuantity
                                    |> Quantity.toValueUnitStringList None
                                    |> Seq.zip (itm.OrderableQuantity
                                                |> Quantity.toVarUnt
                                                |> VariableUnit.getValues)

                                match da |> Seq.tryHead, iq |> Seq.tryHead, oq |> Seq.tryHead with
                                | Some da, Some (iv, iq), Some (ov, oq) ->
                                    
                                    sprintf "%s %s in %s, %s = %s" sn iq oq rt da, rv, (iv / ov)

                                | _ -> "", 0N, 0N
                
            ]
            |> List.filter (fun (s, _, _) -> s |> String.isNullOrWhiteSpace |> not)
            |> List.sortBy (fun (_, rv, iv) -> iv, rv)
            |> List.map (fun (s, _, _) -> s)
            |> List.distinct

        | _ -> []



// Creating a non fluid drug
module DrugOrder =

        module RouteShape =

            type RouteShape =
                | IntravenousFluid
                | OralFluid
                | OralSolid
                | RectalSolid

            let mapping = 
                [
                    "rect", "supp", RectalSolid
                    "rect", "zetpil", RectalSolid
                    "rectaal", "supp", RectalSolid
                    "rectaal", "zetpil", RectalSolid
                    "or", "tablet", OralSolid
                    "or", "pill", OralSolid
                    "or", "pil", OralSolid
                    "or", "capsule", OralSolid
                    "oraal", "tablet", OralSolid
                    "oraal", "pill", OralSolid
                    "oraal", "pil", OralSolid
                    "oraal", "capsule", OralSolid
                    "or", "drink", OralFluid
                    "or", "drank", OralFluid
                    "iv", "infusion fluid", IntravenousFluid
                ]

            let map route shape =
                mapping
                |> List.find (fun (r, s, _) -> r = route && s = shape )
                |> fun (_, _, x) -> x


        module Constraints =

            module Mapping = Order.Mapping
            module Props = Solver.Props
            module Name = WrappedString.Name
            
    
            type Constraint = 
                | SolidOralOrderableDoseQuantityMax of BigRational
                | SuppositoryOrderableDoseQuantity of BigRational
                | FluidOrderableDoseQuantityMax of BigRational
                | FluidOrderableDoseRateIncr of BigRational
                | FluidOrderableDoseRateMax of BigRational
                | FluidOrderableDoseRateAdjustedMax of BigRational
                | FluidOrderableDoseAdjustQuantityMax of BigRational
                | FluidOrderableQuantityIncrement of BigRational

            let constraints =
                [
                    SolidOralOrderableDoseQuantityMax 10N
                    SuppositoryOrderableDoseQuantity 1N
                    FluidOrderableDoseQuantityMax 30N
                    FluidOrderableDoseAdjustQuantityMax 5N
                    FluidOrderableDoseRateMax 20N
                    FluidOrderableDoseRateAdjustedMax 1N
                ]

            let apply cs shape (o : Order.Order) =
                let n = o.Orderable.Name |> Name.toString
                let os = RouteShape.map o.Route shape
                printfn "setting constraints" 

                cs
                |> List.fold (fun acc c ->
                    match c with
                    | FluidOrderableDoseQuantityMax x -> 
                        os
                        |> function
                        |  RouteShape.OralFluid |  RouteShape.IntravenousFluid ->
                            acc
                            |> Order.solve n Mapping.OrderableDoseQty Props.MaxIncl [x]
                        | _ -> acc
                    | FluidOrderableDoseRateIncr x -> 
                        os
                        |> function
                        |  RouteShape.OralFluid |  RouteShape.IntravenousFluid ->
                            acc
                            |> Order.solve n Mapping.OrderableDoseRate Props.Incr [x]
                        | _ -> acc
                    | FluidOrderableDoseRateMax x -> 
                        os
                        |> function
                        |  RouteShape.OralFluid |  RouteShape.IntravenousFluid ->
                            acc
                            |> Order.solve n Mapping.OrderableDoseRate Props.MaxIncl [x]
                        | _ -> acc
                    | FluidOrderableDoseRateAdjustedMax x -> 
                        os
                        |> function
                        |  RouteShape.OralFluid |  RouteShape.IntravenousFluid ->
                            acc
                            |> Order.solve n Mapping.OrderableDoseAdjustRateAdjust Props.MaxIncl [x]
                        | _ -> acc
                    | FluidOrderableDoseAdjustQuantityMax x -> 
                        os
                        |> function
                        |  RouteShape.OralFluid |  RouteShape.IntravenousFluid ->
                            acc
                            |> Order.solve n Mapping.OrderableDoseAdjustQtyAdjust Props.MaxIncl [x]
                        | _ -> acc
                    | FluidOrderableQuantityIncrement x ->
                        os
                        |> function 
                        | RouteShape.OralFluid | RouteShape.IntravenousFluid ->
                            acc.Orderable.Components
                            |> List.fold (fun acc c ->
                                let n = 
                                    c.Name
                                    |> Name.toString
                                acc
                                |> Order.solve n Mapping.ComponentOrderableQty Props.Incr [ x ]
                            ) acc
                        | _ -> acc
                    | SuppositoryOrderableDoseQuantity x -> 
                        os
                        |> function
                        |  RouteShape.RectalSolid ->
                            acc
                            |> Order.solve n Mapping.OrderableDoseQty Props.Vals [x]
                        | _ -> acc
                    | SolidOralOrderableDoseQuantityMax x -> 
                        os
                        |> function
                        |  RouteShape.OralSolid ->                
                            acc
                            |> Order.solve n Mapping.OrderableDoseQty Props.MaxIncl [x]
                        | _ -> acc

                ) o

        module Item = Orderable.Item
        module IDto = Item.Dto
        module Component = Orderable.Component
        module CDto = Component.Dto
        module ODto = Orderable.Dto

        module Mapping = Order.Mapping
        module Props = Solver.Props

        type OrderType =
            | Process
            | Continuous
            | Discontinuous
            | Timed

                
        type DrugOrder =
            {
                Id:  string
                Name : string
                Products : ProductComponent list
                Quantities :  BigRational list
                Unit : string
                TimeUnit : string
                RateUnit : string
                Shape : string
                Divisible : BigRational
                Route : string
                OrderType : OrderType
            }
        and ProductComponent = 
            { 
                Name : string
                Quantities : BigRational list
                TimeUnit : string
                RateUnit : string
                Substances: SubstanceItem list 
            }
        and SubstanceItem = 
            { 
                Name : string
                Concentrations : BigRational list
                OrderableQuantities : BigRational list
                Unit : string
                DoseUnit : string
                TimeUnit : string
                RateUnit : string
            }

        let drugOrder =
            {
                Id = ""
                Name = ""
                Products = []
                Quantities = []
                Unit = ""
                TimeUnit = ""
                RateUnit = "hour"
                Shape = ""
                Divisible = 1N
                Route = ""
                OrderType = Process
            }

        let productComponent =
            {
                Name = ""
                Quantities = []
                TimeUnit = "day"
                RateUnit = "hour"
                Substances = []
            }

        let substanceItem =
            {
                Name = ""
                Concentrations = []
                OrderableQuantities = []
                Unit = ""
                DoseUnit = ""
                TimeUnit = ""
                RateUnit = ""
            }


        let unitGroup u =
            ValueUnit.Units.units
            |> List.filter (fun ud ->
                ud.Group <> ValueUnit.Group.WeightGroup
            )
            |> List.tryFind (fun ud ->
                ud.Abbreviation.Dut::ud.Abbreviation.Eng::ud.Name.Dut::ud.Name.Eng::ud.Synonyms
                |> List.exists((=) u)
            )
            |> function 
            | Some ud -> 
                ud.Group 
                |> ValueUnit.Group.toString 
            | None -> "General"
            |> sprintf "%s[%s]" u
            

        let create (d : DrugOrder) =
            let ou = d.Unit |> unitGroup
            let odto = ODto.dto d.Id d.Name d.Shape

            odto.OrderableQuantity.Unit <- ou
            odto.OrderQuantity.Unit <- ou
            
            match d.OrderType with
            | Process -> ()
            | Continuous ->                
                odto.DoseRate.Unit <- 
                    d.RateUnit
                    |> unitGroup
                    |> sprintf "%s/%s" ou
            | Discontinuous ->                
                odto.DoseTotal.Unit <-
                    d.TimeUnit
                    |> unitGroup
                    |> sprintf "%s/%s" ou
            | Timed ->                
                odto.DoseTotal.Unit <-
                    d.TimeUnit
                    |> unitGroup
                    |> sprintf "%s/%s" ou
                odto.DoseRate.Unit <- 
                    d.RateUnit
                    |> unitGroup
                    |> sprintf "%s/%s" ou


            odto.Components <- 
                [
                    for p in d.Products do
                        let cdto = CDto.dto d.Id p.Name

                        cdto.Items <- [ 
                            for s in p.Substances do
                                let su = s.Unit |> unitGroup
                                let du = s.DoseUnit |> unitGroup
                                let tu = s.TimeUnit |> unitGroup

                                let idto = IDto.dto d.Id s.Name

                                idto.ComponentConcentration.Unit <- 
                                    sprintf "%s/%s" su ou
                                idto.ComponentQuantity.Unit <- su

                                match d.OrderType with
                                | Process -> ()
                                | Continuous ->
                                    idto.DoseRateAdjust.Unit <- 
                                        sprintf "%s/kg[Weight]/%s" du tu
                                | Discontinuous ->
                                    idto.DoseQuantity.Unit <- du
                                    idto.DoseTotalAdjust.Unit <- 
                                        p.TimeUnit
                                        |> unitGroup
                                        |> sprintf "%s/kg[Weight]/%s" du 
                                | Timed ->
                                    idto.DoseQuantity.Unit <- du
                                    idto.DoseTotalAdjust.Unit <- 
                                        p.TimeUnit
                                        |> unitGroup
                                        |> sprintf "%s/kg[Weight]/%s" du 
                                    idto.DoseRateAdjust.Unit <- 
                                        sprintf "%s/kg[Weight]/%s" du tu
                                
                                idto                
                        ]

                        cdto.OrderableQuantity.Unit <- ou
                        cdto.OrderableConcentration.Unit <- "x[Count]"
                        cdto.OrderQuantity.Unit <- ou

                        cdto                        
                ]

            let dto = 
                match d.OrderType with
                | Process ->
                    Order.Dto.``process`` d.Id d.Name d.Shape d.Route
                | Continuous ->
                    Order.Dto.continuous d.Id d.Name d.Shape d.Route
                | Discontinuous ->
                    Order.Dto.discontinuous d.Id d.Name d.Shape d.Route
                | Timed ->
                    Order.Dto.timed d.Id d.Name d.Shape d.Route

            dto.Orderable <- odto

            dto.Prescription.Frequency.Unit <- 
                sprintf "x[Count]/%s" (d.TimeUnit |> unitGroup)
            dto.Adjust.Unit <- "kg[Weight]"

            let rs = RouteShape.map d.Route d.Shape

            dto
            |> Order.Dto.fromDto
            |> Order.solve d.Name Mapping.OrderableDoseQty Props.Incr [ 1N / d.Divisible ]
            |> fun o ->
                match rs with
                | RouteShape.OralSolid
                | RouteShape.RectalSolid ->
                    o
                    |> Order.solve d.Name Mapping.OrderableOrderableQty Props.Vals [ 1N ]
                | _ -> 
                    o
                    |> Order.solve d.Name Mapping.OrderableOrderableQty Props.Vals d.Quantities
                            
            |> fun o ->
                d.Products
                |> Seq.fold (fun o p ->
                    let o =
                        match rs with
                        | RouteShape.OralSolid 
                        | RouteShape.RectalSolid
                        | _ when d.Products |> List.length = 1 ->
                            o
                            |> Order.solve p.Name Mapping.ComponentOrderableConc Props.Vals [ 1N ]
                        | _ -> o
                        |> Order.solve p.Name Mapping.ComponentComponentQty Props.Vals p.Quantities

                    p.Substances 
                    |> Seq.fold (fun o s ->
                        o
                        |> Order.solve s.Name Mapping.ItemComponentConc Props.Vals s.Concentrations 
                        |> Order.solve s.Name Mapping.ItemOrderableQty Props.Vals s.OrderableQuantities 
                        |> (fun o ->
                            match rs with
                            | RouteShape.OralSolid
                            | RouteShape.RectalSolid ->
                                o
                                |> Order.solve s.Name Mapping.ItemOrderableConc Props.Vals s.Concentrations
                            | _ -> o
                        )                   
                    ) o
                ) o
                

        type DoseLimits =
            {
                Name : string
                Frequencies : BigRational list
                SubstanceName : string
                MaxDoseQuantity : BigRational option
                MinDoseQuantity : BigRational option
                MaxDoseQuantityAdjust : BigRational option
                MinDoseQuantityAdjust : BigRational option
                MaxDoseTotal : BigRational option
                MinDoseTotal : BigRational option
                MaxDoseTotalAdjust : BigRational option
                MinDoseTotalAdjust : BigRational option
                MaxDoseRate : BigRational option
                MinDoseRate : BigRational option
                MaxDoseRateAdjust : BigRational option
                MinDoseRateAdjust : BigRational option
            }

        let doseLimits =
            {
                Name = ""
                Frequencies = []
                SubstanceName = ""
                MaxDoseQuantity = None
                MinDoseQuantity = None
                MinDoseQuantityAdjust = None
                MaxDoseQuantityAdjust = None
                MaxDoseTotal = None
                MinDoseTotal = None
                MaxDoseTotalAdjust = None
                MinDoseTotalAdjust = None
                MaxDoseRate = None
                MinDoseRate = None
                MaxDoseRateAdjust = None
                MinDoseRateAdjust = None
            }

        let setConstraints constraints (o : Order.Order) =
            o
            |> Constraints.apply constraints o.Orderable.Shape

        let setDoseLimits (dl : DoseLimits) o =
            let set m p l o =
                match l with
                | Some l -> 
                    o
                    |> Order.solve dl.SubstanceName m p [ l ]
                | None -> o
                    
            o
            |> Order.solve dl.Name Mapping.Freq Props.Vals dl.Frequencies
            |> set Mapping.ItemDoseQty Props.MaxIncl dl.MaxDoseQuantity
            |> set Mapping.ItemDoseQty Props.MinIncl dl.MinDoseQuantity
            |> set Mapping.ItemDoseAdjustQtyAdjust Props.MaxIncl dl.MaxDoseQuantityAdjust
            |> set Mapping.ItemDoseAdjustQtyAdjust Props.MinIncl dl.MinDoseQuantityAdjust
            |> set Mapping.ItemDoseTotal Props.MaxIncl dl.MaxDoseTotal
            |> set Mapping.ItemDoseTotal Props.MinIncl dl.MinDoseTotal
            |> set Mapping.ItemDoseAdjustTotalAdjust Props.MaxIncl dl.MaxDoseTotalAdjust
            |> set Mapping.ItemDoseAdjustTotalAdjust Props.MinIncl dl.MinDoseTotalAdjust
            |> set Mapping.ItemDoseRate Props.MaxIncl dl.MaxDoseRate
            |> set Mapping.ItemDoseRate Props.MinIncl dl.MinDoseRate
            |> set Mapping.ItemDoseAdjustRateAdjust Props.MaxIncl dl.MaxDoseRateAdjust
            |> set Mapping.ItemDoseAdjustRateAdjust Props.MinIncl dl.MinDoseRateAdjust


        let setAdjust n a o =
            o
            |> Order.solve n Mapping.AdjustQty Props.Vals [a]


module Constraints = DrugOrder.Constraints


// Paracetamol supp
{
    DrugOrder.drugOrder with
        Id = "1"
        Name = "paracetamol"
        Products = 
            [
                { 
                    DrugOrder.productComponent with 
                        Name = "paracetamol"
                        Quantities = [ 1N ]
                        TimeUnit = "day"
                        Substances =
                            [
                                {
                                    DrugOrder.substanceItem with
                                        Name = "paracetamol"
                                        Concentrations = 
                                            [ 60N; 120N; 240N; 500N; 1000N ]
                                        Unit = "mg"
                                        DoseUnit = "mg"
                                        TimeUnit = "day"
                                }
                            ]
                }
            ]
        Unit = "piece"
        TimeUnit = "day"
        Shape = "supp"
        Route = "rect"
        OrderType = DrugOrder.Discontinuous
}
|> DrugOrder.create
|> DrugOrder.setConstraints Constraints.constraints
|> DrugOrder.setDoseLimits
    {   DrugOrder.doseLimits with
            Name = "paracetamol"
            Frequencies = [ 2N .. 4N ]
            SubstanceName = "paracetamol"
            MaxDoseQuantity = Some 1000N
            MaxDoseTotal = Some 4000N
            MaxDoseTotalAdjust = Some 90N
    }
|> DrugOrder.setAdjust "paracetamol" 10N
|> Order.printPrescriptions "paracetamol"


// Drug with multiple items
// cotrimoxazol for infection
{
    DrugOrder.drugOrder with
        Id = "1"
        Name = "cotrimoxazol"
        Products = 
            [
                { 
                    DrugOrder.productComponent with 
                        Name = "cotrimoxazol"
                        Quantities = [ 1N ]
                        TimeUnit = "day"
                        Substances =
                            [
                                {
                                    DrugOrder.substanceItem with
                                        Name = "sulfamethoxazol"
                                        Concentrations = 
                                            [ 100N; 400N; 800N ]
                                        Unit = "mg"
                                        DoseUnit = "mg"
                                        TimeUnit = "day"
                                }
                                {
                                    DrugOrder.substanceItem with
                                        Name = "trimethoprim"
                                        Concentrations = 
                                            [ 20N; 80N; 160N ]
                                        Unit = "mg"
                                        DoseUnit = "mg"
                                        TimeUnit = "day"
                                }
                            ]
                }
            ]
        Unit = "piece"
        TimeUnit = "day"
        Shape = "tablet"
        Route = "or"
        OrderType = DrugOrder.Discontinuous
}
|> DrugOrder.create
|> DrugOrder.setConstraints 
    (Constraints.constraints 
     |> List.map (fun c ->
        match c with
        | Constraints.SolidOralOrderableDoseQuantityMax _ ->
            Constraints.SolidOralOrderableDoseQuantityMax 2N
        | _ -> c
     ))
// setting dose limits for infection
|> DrugOrder.setDoseLimits
    {   DrugOrder.doseLimits with
            Name = "cotrimoxazol"
            Frequencies = [ 2N ]
            SubstanceName = "sulfamethoxazol"
            MaxDoseTotal = Some 1600N
            MaxDoseTotalAdjust = Some 30N
    }
|> DrugOrder.setAdjust "cotrimoxazol" 10N
// is not be necessary when a single product is chosen
|> DrugOrder.setDoseLimits
    {   DrugOrder.doseLimits with
            Name = "cotrimoxazol"
            Frequencies = [ 2N ]
            SubstanceName = "trimethoprim"
            MaxDoseTotal = Some 320N
            MaxDoseTotalAdjust = Some 6N
    }
|> Order.toString 
|> List.iteri (fun i s -> printfn "%i\t%s" i s)



// Paracetamol drink
{
    DrugOrder.drugOrder with
        Id = "1"
        Name = "paracetamol"
        Products = 
            [
                { 
                    DrugOrder.productComponent with 
                        Name = "paracetamol"
                        Quantities = [ 100N ]
                        TimeUnit = "day"
                        Substances =
                            [
                                {
                                    DrugOrder.substanceItem with
                                        Name = "paracetamol"
                                        Concentrations = [ 24N ]
                                        Unit = "mg"
                                        DoseUnit = "mg"
                                        TimeUnit = "day"
                                }
                            ]
                }
            ]
        Unit = "ml"
        TimeUnit = "day"
        Shape = "drink"
        Route = "or"
        OrderType = DrugOrder.Discontinuous
}
|> DrugOrder.create
|> DrugOrder.setAdjust "paracetamol" 20N
|> DrugOrder.setConstraints Constraints.constraints
|> DrugOrder.setDoseLimits
    {   DrugOrder.doseLimits with
            Name = "paracetamol"
            Frequencies = [ 4N ]
            SubstanceName = "paracetamol"
            MaxDoseQuantity = Some 1000N
            MaxDoseTotal = Some 4000N
            MinDoseTotalAdjust = Some 20N
            MaxDoseTotalAdjust = Some 90N
    }
|> Order.printPrescriptions "paracetamol"
//|> List.iteri (fun i s -> printfn "%i\t%s" i s)



// Dopamin infusion calculate scenario's 
// with a number of standard solutions
{
    DrugOrder.drugOrder with
        Id = "1"
        Name = "dopamin infusion"
        Quantities = [ 24N; 50N ]
        Unit = "ml"
        TimeUnit = "day"
        Shape = "infusion fluid"
        Route = "iv"
        Products = 
            [
                { 
                    DrugOrder.productComponent with
                        Name = "dopamin"
                        Quantities = [ 5N ]
                        TimeUnit = "day"
                        Substances = 
                            [
                                {
                                    DrugOrder.substanceItem with
                                        Name = "dopamin"
                                        Concentrations = [ 40N ]
                                        OrderableQuantities = [ 80N; 200N; 400N ]
                                        Unit = "mg"
                                        DoseUnit = "mcg"
                                        TimeUnit = "min"
                                }
                            ]

                }
                { 
                    DrugOrder.productComponent with
                        Name = "saline"
                        Quantities = [ 5000N ]
                        TimeUnit = "day"
                        Substances = 
                            [
                                {
                                    DrugOrder.substanceItem with
                                        Name = "sodium"
                                        Concentrations = [ 155N / 1000N ]
                                        Unit = "mmol"
                                        DoseUnit = "mmol"
                                        TimeUnit = "day"
                                }
                                {
                                    DrugOrder.substanceItem with
                                        Name = "chloride"
                                        Concentrations = [ 155N / 1000N ]
                                        Unit = "mmol"
                                        DoseUnit = "mmol"
                                        TimeUnit = "day"
                                }
                            ]

                }
            ]
        OrderType = DrugOrder.Continuous
}
|> DrugOrder.create
|> Order.Dto.toDto
|> Order.Dto.setToContinuous
|> Order.Dto.fromDto
|> DrugOrder.setAdjust "dopamin infusion" 10N
|> DrugOrder.setConstraints Constraints.constraints
|> DrugOrder.setDoseLimits
    {   DrugOrder.doseLimits with
            Name = "dopamin infusion"
            SubstanceName = "dopamin"
            MaxDoseRateAdjust = Some 20N
    }
|> DrugOrder.setAdjust "dopamin infusion" 10N
|> DrugOrder.setConstraints [ DrugOrder.Constraints.FluidOrderableDoseRateIncr (1N / 10N) ]
|> Order.printPrescriptions "dopamin"
|> List.iteri (fun i s -> printfn "%i\t%s" (i + 1) s)


// Dopamin infusion calculate scenario's 
// with a a fixed infusion - dose rate
{
    DrugOrder.drugOrder with
        Id = "1"
        Name = "dopamin infusion"
        Quantities = [ 50N ]
        Unit = "ml"
        TimeUnit = "day"
        Shape = "infusion fluid"
        Route = "iv"
        Products = 
            [
                { 
                    DrugOrder.productComponent with
                        Name = "dopamin"
                        Quantities = [ 5N ]
                        TimeUnit = "day"
                        Substances = 
                            [
                                {
                                    DrugOrder.substanceItem with
                                        Name = "dopamin"
                                        Concentrations = [ 40N ]
//                                        OrderableQuantities = [ 80N; 200N; 400N ]
                                        Unit = "mg"
                                        DoseUnit = "mcg"
                                        TimeUnit = "min"
                                }
                            ]

                }
                { 
                    DrugOrder.productComponent with
                        Name = "saline"
                        Quantities = [ 5000N ]
                        TimeUnit = "day"
                        Substances = 
                            [
                                {
                                    DrugOrder.substanceItem with
                                        Name = "sodium"
                                        Concentrations = [ 155N / 1000N ]
                                        Unit = "mmol"
                                        DoseUnit = "mmol"
                                        TimeUnit = "day"
                                }
                                {
                                    DrugOrder.substanceItem with
                                        Name = "chloride"
                                        Concentrations = [ 155N / 1000N ]
                                        Unit = "mmol"
                                        DoseUnit = "mmol"
                                        TimeUnit = "day"
                                }
                            ]

                }
            ]
        OrderType = DrugOrder.Continuous
}
|> DrugOrder.create
|> Order.Dto.toDto
|> Order.Dto.setToContinuous
|> Order.Dto.fromDto
|> DrugOrder.setAdjust "dopamin infusion" 10N
|> DrugOrder.setConstraints Constraints.constraints
|> DrugOrder.setDoseLimits
    {   DrugOrder.doseLimits with
            Name = "dopamin infusion"
            SubstanceName = "dopamin"
//            MinDoseRateAdjust = Some 1N
            MaxDoseRateAdjust = Some 20N
    }
|> DrugOrder.setAdjust "dopamin infusion" 10N
|> Order.solve "dopamin infusion" Order.Mapping.OrderableDoseRate Solver.Props.Vals [ 1N ]
|> Order.solve "dopamin" Order.Mapping.ComponentOrderableQty Solver.Props.Incr [ 1N/10N ]
|> Order.printPrescriptions "dopamin"
|> List.iteri (fun i s -> printfn "%i\t%s" (i + 1) s)

