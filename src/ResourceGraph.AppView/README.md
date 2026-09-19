# AppView boundary

`ResourceGraph.AppView` contains only public-read projection contracts in this
slice. It does not connect to a firehose, PDS, database, or hosted service.
Those concerns belong to a later optional topology after direct-pull reader
behavior is proven.
