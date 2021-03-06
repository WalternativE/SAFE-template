module SAFE.Tests

open System
open System.IO

open Expecto
open Expecto.Logging
open Expecto.Logging.Message

open FsCheck

open Fake.Core
open Fake.IO
open Fake.IO.FileSystemOperators

let dotnet =
    match Environment.GetEnvironmentVariable "DOTNET_PATH" with
    | null -> "dotnet"
    | x -> x

let fake =
    match Environment.GetEnvironmentVariable "FAKE_PATH" with
    | null -> "fake"
    | x -> x

let maxTests =
    match Environment.GetEnvironmentVariable "MAX_TESTS" with
    | null -> 20
    | x ->
        match System.Int32.TryParse x with
        | true, n -> n
        | _ -> 20

let psi exe arg dir (x: ProcStartInfo) : ProcStartInfo =
    { x with
        FileName = exe
        Arguments = arg
        WorkingDirectory = dir }

let logger = Log.create "SAFE"

type TemplateArgs =
    { Server : string option
      Deploy : string option
      Layout : string option
      JsDeps : string option
      Communication : string option
      Pattern : string option }

    override args.ToString () =
        let optArg (name, value) =
            value
            |> Option.map (sprintf "--%s %s" name)

        [ "server", args.Server
          "deploy", args.Deploy
          "layout", args.Layout
          "js-deps", args.JsDeps
          "communication", args.Communication
          "pattern", args.Pattern ]
        |> List.map optArg
        |> List.choose id
        |> String.concat " "

let serverGen =
    Gen.elements [
        None
        Some "giraffe"
        Some "suave"
    ]

let deployGen =
    Gen.elements [
        None
        Some "docker"
        Some "azure"
    ]

let layoutGen =
    Gen.elements [
        None
        Some "fulma-basic"
        Some "fulma-admin"
        Some "fulma-cover"
        Some "fulma-hero"
        Some "fulma-landing"
        Some "fulma-login"
    ]

let jsDepsGen =
    Gen.elements [
        None
        Some "npm"
    ]

let communicationGen =
    Gen.elements [
        None
        Some "remoting"
    ]

let patternGen =
    Gen.elements [
        None
        Some "reaction"
    ]

type TemplateArgsArb () =
    static member Arb () : Arbitrary<TemplateArgs> =
        let generator : Gen<TemplateArgs> =
            gen {
                let! server = serverGen
                let! deploy = deployGen
                let! layout = layoutGen
                let! jsDeps = jsDepsGen
                let! communication = communicationGen
                let! pattern = patternGen
                return
                    { Server = server
                      Deploy = deploy
                      Layout = layout
                      JsDeps = jsDeps
                      Communication = communication
                      Pattern = pattern }
            }

        let shrinker (x : TemplateArgs) : seq<TemplateArgs> =
            seq {
                match x.Server with
                | Some _ -> yield { x with Server = None }
                | _ -> ()
                match x.Deploy with
                | Some _ -> yield { x with Deploy = None }
                | _ -> ()
                match x.Layout with
                | Some _ -> yield { x with Layout = None }
                | _ -> ()
                match x.JsDeps with
                | Some _ -> yield { x with JsDeps = None }
                | _ -> ()
                match x.Communication with
                | Some _ -> yield { x with Communication = None }
                | _ -> ()
                match x.Pattern with
                | Some _ -> yield { x with Pattern = None }
                | _ -> ()
            }

        Arb.fromGenShrink (generator, shrinker)


let run exe arg dir =
    logger.info(
        eventX "Running `{exe} {arg}` in `{dir}`"
        >> setField "exe" exe
        >> setField "arg" arg
        >> setField "dir" dir)

    let result = Process.execWithResult (psi exe arg dir) TimeSpan.MaxValue
    Expect.isTrue (result.OK) (sprintf "`%s %s` failed: %A" exe arg result.Errors)

let fsCheckConfig =
    { FsCheckConfig.defaultConfig with
        arbitrary = [typeof<TemplateArgsArb>]
        maxTest = maxTests }

[<Tests>]
let tests =
    testList "Project created from template" [
        testPropertyWithConfig fsCheckConfig "Project should build properly" (fun (x : TemplateArgs) ->
            let newSAFEArgs = x.ToString()
            let uid = Guid.NewGuid().ToString("n")
            let dir = Path.GetTempPath() </> uid
            Directory.create dir

            run dotnet (sprintf "new SAFE %s" newSAFEArgs) dir

            Expect.isTrue (File.exists (dir </> "paket.lock")) (sprintf "paket.lock not present for '%s'" newSAFEArgs)

            run fake "build" dir

            logger.info(
                eventX "Deleting `{dir}`"
                >> setField "dir" dir)
            Directory.delete dir
        )
    ]
