
namespace FilterConstructor

open Elmish
open Fable.Helpers.ReactNative
open Fable.Helpers.ReactNative.Props
open SelectModal
open Select
open Fable.Import
open Fable.Import.ReactNative

module R = Fable.Helpers.React
module RN = Fable.Helpers.ReactNative
module RNP = Fable.Import.ReactNativePortal
module RNF = Fable.Import.ReactNativeColorMatrixImageFilters
module RNS = Fable.Helpers.ReactNativeSegmentedControlTab


module FilteredImage =

  type Id = int

  type Model = 
    { Image: Image.Model
      FilterControls: (Id * CombinedFilterControl.Model * FilterControl.Model) list
      ImageSelectModalIsVisible: bool
      FilterSelectModalIsVisible: bool
      SelectedResizeMode: ResizeMode
      NextId: Id }

  type Message =
    | Delete
    | ImageSelectModalMessage of ImageSelectModal.Message
    | SelectImage
    | FilterSelectModalMessage of FilterSelectModal.Message
    | SelectFilter
    | FilterMessage of Id * FilterControl.Message
    | ResizeModeChanged of int
    | CopyCode


  let private resizeModes =
    [| ResizeMode.Contain
       ResizeMode.Cover
       ResizeMode.Stretch
       ResizeMode.Center
       ResizeMode.Repeat |]

  let private resizeControlValues = new ResizeArray<string> (Array.map unbox<string> resizeModes)

  let init image =
    { Image = image
      FilterControls = []
      ImageSelectModalIsVisible = false 
      FilterSelectModalIsVisible = false
      SelectedResizeMode = ResizeMode.Contain
      NextId = 0 }

  let selectImage model image =
    { model with Image = image }

  let private resizeControlIndex model =
    match Array.tryFindIndex (fun x -> x = model.SelectedResizeMode) resizeModes with
    | Some x -> x
    | None -> 0

  let update (message: Message) model =
    match message with
    | Delete ->
      model, []

    | ImageSelectModalMessage msg ->
      match msg with
      | SelectMessage (ItemSelected image) -> 
        (selectImage model image), []
      | Hide ->
        { model with ImageSelectModalIsVisible = false }, []

    | SelectImage ->
      { model with ImageSelectModalIsVisible = true }, []

    | FilterSelectModalMessage msg ->
      match msg with
      | SelectMessage (ItemSelected filter) -> 
        { model with FilterControls = model.FilterControls @ [model.NextId, filter, CombinedFilterControl.init filter]
                     NextId = model.NextId + 1 }, []
      | Hide ->
        { model with FilterSelectModalIsVisible = false }, []

    | SelectFilter ->
      { model with FilterSelectModalIsVisible = true }, []

    | FilterMessage (id, msg) ->
      match List.tryFind (fun (i, _, _) -> i = id) model.FilterControls with
      | None -> model, []
      | Some (_, _, filter) ->
        let filter', cmd = FilterControl.update msg filter
        let filters = List.map (fun (i, t, f) -> i, t, if i = id then filter' else f) model.FilterControls
        let filters' =
          match msg with
          | FilterControl.Message.Delete ->
            List.filter (fun (i, _, _) -> i <> id) filters
          | FilterControl.Message.MoveDown ->
            Utils.moveUpAt (List.findIndex (fun (i, _, _) -> i = id) filters) filters
          | FilterControl.Message.MoveUp ->
            Utils.moveDownAt (List.findIndex (fun (i, _, _) -> i = id) filters) filters
          | _ -> filters

        { model with FilterControls = filters' },
        Cmd.map (fun sub -> FilterMessage (id, sub)) cmd

    | ResizeModeChanged index ->
      { model with SelectedResizeMode = resizeModes.[index] }, []

    | CopyCode ->
      Globals.Clipboard.setString "test"
      Alert.alert ("Message", "JS code copied to clipboard", [])
      model, []
      


  let private containerStyle =
    ViewProperties.Style
      [ MarginTop (dip 5.)
        Padding (dip 5.)
        BorderWidth 2.
        BorderRadius 3.
        BackgroundColor "white" ]

  let private imageStyle =
    ImageProperties.Style
      [ MarginBottom (dip 5.)
        Width (pct 100.)
        Height (dip Constants.imageHeight) ]

  let private controlsStyle =
    ViewProperties.Style
      [ MarginTop (dip 10.)
        FlexDirection FlexDirection.Row
        JustifyContent JustifyContent.SpaceBetween ]

  let private spinnerStyle =
    ViewProperties.Style
      [ Position Position.Absolute
        Width (pct 100.) 
        Height (dip Constants.imageHeight)
        FlexStyle.Left (dip 5.)
        FlexStyle.Top (dip 5.) ]

  let private controls model dispatch =
    model.FilterControls
    |> List.rev
    |> List.map
       (fun (id, tag, filter) ->
         R.fragment
           [ R.Props.FragmentProp.Key (string id) ]
           [ CombinedFilterControl.view tag filter (fun msg -> dispatch (id, msg)) ])
    |> R.fragment []
  
  let private combinedMatrix model =
    model.FilterControls
    |> List.map (fun (_, control, model) -> CombinedFilterControl.matrix control model)
    |> List.toArray
    |> RNF.concatColorMatrices

  let private filteredImage model =
    RNF.ColorMatrix
      [ RNF.Props.ColorMatrixProps.Matrix (combinedMatrix model) ]
      [ RN.image
          [ imageStyle
            ResizeMode model.SelectedResizeMode
            Source (Image.source model.Image) ] ]
      
  let view model (dispatch: Dispatch<Message>) =
    R.fragment
      []
      [ RNP.enterPortal
          Constants.imagePortal
          [ ImageSelectModal.view
              model.Image
              model.ImageSelectModalIsVisible
              (ImageSelectModalMessage >> dispatch) ]
        RNP.enterPortal
          Constants.filterPortal
          [ FilterSelectModal.view
              model.FilterSelectModalIsVisible
              (FilterSelectModalMessage >> dispatch) ]
        RN.view
          [ containerStyle
            ActivityIndicator.Size Size.Large ]
          [ RN.button
              [ ButtonProperties.Title "Add filter"
                ButtonProperties.OnPress (fun () -> dispatch SelectFilter) ]
              []
            Spacer.view 5.
            (controls model (FilterMessage >> dispatch))
            RN.view
              []
              [ RN.activityIndicator
                  [ spinnerStyle ]
                (filteredImage model) ]
            RNS.segmentedControlTab
              [ RNS.Props.Values resizeControlValues
                RNS.Props.OnTabPress (ResizeModeChanged >> dispatch)
                RNS.Props.SelectedIndex (resizeControlIndex model) ]
            RN.view
              [ controlsStyle ]
              [ RN.button
                  [ ButtonProperties.Title "Copy code"
                    ButtonProperties.OnPress (fun () -> dispatch CopyCode) ]
                  [] 
                RN.button
                  [ ButtonProperties.Title "Change image"
                    ButtonProperties.OnPress (fun () -> dispatch SelectImage) ]
                  [] 
                RN.button
                  [ ButtonProperties.Title "Delete"
                    ButtonProperties.Color "red"
                    ButtonProperties.OnPress (fun () -> dispatch Delete) ]
                  [] ] ] ]
