# The .NET Show Episode 9

###### Building a Mobile Podcast App Part 7

See more projects at https://github.com/carlfranklin/DotNetShow

Watch the video at 

All episodes are listed at https://thedotnetshow.com

## Overview

Starting with episode 2 of The .NET Show, I am building a mobile podcast app for my podcast, .NET Rocks! using Xamarin Forms. 

At this point our app has a home page that shows episodes. When the user taps the "Details" button, we show a detail page with sophisticated media control: Play, Pause, Stop, Go Back, and Go Forward. We can go create and delete PlayLists, but that's all we can do with them.

In this episode we are going to continue the Playlist feature. Here are the user stories. Completed user stories are in italic

- *As a user, I want to create a new playlist, and give it a name.*
- *As a user, I want the app to automatically persist my playlists.*
- *As a user, I want to be able to retrieve a playlist from the list of playlists I have created.*
- *As a user, I want to delete an existing playlist.*
- As a user, I want to select one or more episodes to add to a playlist.
- As a user, I want to be able to remove episodes from my playlist.
- As a user, I want to be able to move episodes in my playlist forward or backward in play order.
- As a user, I want the same level of audio control for a playlist as I have for a single episode: Play, Pause, Stop, Go Back, and Go Forward.

### BUT FIRST!

We must fix a bug in last-week's code.

In *PlayListManagerPageViewModel.cs*, check out the following method:

```c#
public async Task PerformDeletePlayList(Guid Id)
{
    await Task.Delay(0);
    var existing = (from x in PlayLists where x.Id == Id select x).FirstOrDefault();
    if (existing != null)
    {
        DeletePlayList(existing);
    }
    base.OnPropertyChanged("PlayLists");
}
```

Do you see the bug? We're supposed to be deleting the `PlayList` but we ***never actually delete the file!***

Change it to this:

```c#
public async Task PerformDeletePlayList(Guid Id)
{
    await Task.Delay(0);
    var existing = (from x in PlayLists where x.Id == Id select x).FirstOrDefault();
    if (existing != null)
    {
        string FileName = $"{CacheDir}/{Id}.json";
        if (System.IO.File.Exists(FileName))
        {
            System.IO.File.Delete(FileName);
        }
        DeletePlayList(existing);
    }
    base.OnPropertyChanged("PlayLists");
}
```

OK, now back to our regularly-scheduled program

### Step 33: Add an Episode Filter

There are over 1700 shows in the archives. If we are to select shows to add to a playlist, we probably want to filter them by title based on an input string.

#### Modify ApiService

Add the following method to *ApiService.cs*:

```c#
public async Task<List<Show>> GetFilteredShows(string ShowTitle, 
                                               int StartIndex, int Count)
{
    string Url = $"{ShowName}/{ShowTitle}/{StartIndex}/{Count}/getfilteredshows";
    var result = await httpClient.GetAsync(Url);
    result.EnsureSuccessStatusCode();
    var response = await result.Content.ReadAsStringAsync();
    return JsonConvert.DeserializeObject<List<Show>>(response);
}
```

This method calls into the API to get a set of shows where the title contains the `ShowTitle` (case-insensitive), and then returns just a subset of the result set, up to `Count` items starting at `StartIndex`.

#### Modify HomePageViewModel

To *HomePageViewModel.cs*, add the following:

```c#
private string episodeFilter = "";
public string EpisodeFilter
{
    get
    {
        return episodeFilter;
    }
    set
    {
        episodeFilter = value;
        LastShowNumber = 0;
        var t = Task.Run(() => GetNextBatchOfShows());
        t.Wait();
        base.OnPropertyChanged("EpisodeFilter");
    }
}
```

We will bind this property to an `<Entry>` element in the UI. Whenever the user enters a key, the `set` code will execute.  Basically, we are calling `GetNextBatchOfShows()`, with the added logic to clear the `AllShows` list if the value is not an empty string.  We will modify `GetNextBatchOfShows()` to perform the filtered get if `EpisodeFilter` is not an empty string.

In order for this to work dynamically, we need to change `AllShows` to `ObservableCollection<Show>`

```c#
private ObservableCollection<Show> allShows = new ObservableCollection<Show>();
public ObservableCollection<Show> AllShows
{
    get => allShows;
    set => SetProperty(ref allShows, value);
}
```

You'll need this:

```c#
using System.Collections.ObjectModel;
```

Next, add this:

```c#
public async Task GetNextBatchOfFilteredShows()
{
    var nextBatch = await ApiService.GetFilteredShows(EpisodeFilter, AllShows.Count, 20);
    if (nextBatch == null || nextBatch.Count == 0) return;
    foreach (var show in nextBatch)
    {
        AllShows.Add(show);
    }
    base.OnPropertyChanged("AllShows");
}
```

We will call this from `GetNextBatchOfShows()`. If we are entering a filter, we will use `AllShows.Count` as our starting index, and retrieve up to 20 shows. Notice that we have to add the shows one at a time, because `ObservableCollection` doesn't have an `AddRange()` method.

Here's where we make some magic happen.

Add the following code to the top of `GetNextBatchOfShows()` :

```c#
if (EpisodeFilter != "")
{
    AllShows.Clear();
    await GetNextBatchOfFilteredShows();
    return;
}
```

Our existing code stays pretty much the same. We're just diverting to `GetNextBatchOfFilteredShows()` if there is something to filter on.

Now let's clean up one more thing.

In `GetNextBatchOfShows()` change the following line:

```c#
AllShows.AddRange(nextBatch);
```

to this:

```c#
foreach (var show in nextBatch)
{
    AllShows.Add(show);
}
```

#### Modify HomePage.xaml

Add the following just below the label that says ".NET Rocks! Podcast Client":

```Xaml
<StackLayout Orientation="Horizontal" Margin="10,0,0,0">
    <Label FontSize="Large"
           TextColor="Black"
           VerticalTextAlignment="Center"
           Text="Filter:" />
    <Entry HorizontalOptions="StartAndExpand" 
           VerticalTextAlignment="Center"
           WidthRequest="200"
           Text="{Binding EpisodeFilter}" />
</StackLayout>
<Line Stroke="Gray" X1="0" X2="500" StrokeThickness="2" Margin="0,10,0,10" />
```

Give it a go!

<img src="ModalDialogs Images/image-20210729224746343.png" alt="image-20210729224746343" style="zoom:70%;" />

Try entering the word "Azure" in the `<Entry>` element:

<img src="ModalDialogs Images/image-20210729224816747.png" alt="image-20210729224816747" style="zoom:70%;" />

The **LOAD MORE SHOWS** button will retrieve up to the next 20 shows in the filtered list.

Backspace to remove the filter, and you're back looking at the top 20 shows.

### PlayList Management Behavior

Let's talk through how we want this flow to behave.

This is what our `PlayListManagerPage` looks like with one `PlayList` defined:

<img src="ModalDialogs Images/image-20210729223952418.png" alt="image-20210729223952418" style="zoom:70%;" />

So, to the left of the **DELETE** button, we'll add an **ADD/REMOVE** button that will bring us back to the `HomePage` passing the Id of the selected `PlayList`, which the `HomePage` will retrieve from storage.

For each show, the `HomePage` will show either a **SELECT** button or a **REMOVE** button to the left of the **EPISODE DETAILS** button.

After selecting a show, it will be added to the `PlayList`'s `Shows` list, and the **SELECT** button text will change to **REMOVE**. Tapping that button will remove it from the `Shows` list.

#### Add the ADD/REMOVE feature to the PlayListManagerPage

Add the following to *PlayListManagerPageViewModel.cs* :

```c#
private ICommand addRemove;
public ICommand AddRemove
{
    get
    {
        if (addRemove == null)
        {
            addRemove = new AsyncCommand<Guid>(PerformAddRemove);
        }
        return addRemove;
    }
}

public async Task PerformAddRemove(Guid PlayListId)
{
    await Shell.Current.GoToAsync($"HomePage?PlayListId={PlayListId}");
}
```

This is a nice introduction to navigating to a page passing parameters.

Our **ADD/REMOVE** button will pass the `PlayList`'s Id property, which we can pass to the `HomePage` just like passing parameters using HTTP.

The `Guid` is passed as a string. 

To  *PlayListManagerPage.xaml* add the following just above the **DELETE** button definition:

```xaml
<Button Text="Add/Remove" 
        Command="{Binding AddRemove,
                 Source={RelativeSource AncestorType=
                 {x:Type viewmodels:PlayListManagerPageViewModel}}}" 
        CommandParameter="{Binding Id}" />
```

#### Modify the HomePageViewModel

Now, we need to modify *HomePageViewModel.cs* to handle the parameter.

First, we need a few using statements:

```c#
using Xamarin.Essentials;
using MonkeyCache.FileStore;
using Newtonsoft.Json;
using System.Web;
```

Add the following line to the top of the class:

```c#
private string CacheDir = "";
```

Since we need to load the `PlayList` from storage, we need to modify the constructor to set the Barrel Application Id, and also set the cache directory:

```c#
public HomePageViewModel()
{
    Barrel.ApplicationId = "mobile_dnr";
    CacheDir = FileSystem.CacheDirectory + "/playlists";
    var t = Task.Run(() => GetNextBatchOfShows());
    t.Wait();
}
```

Let's add a `SelectedPlayList` property:

```c#
private PlayList selectedPlayList = null;
public PlayList SelectedPlayList
{
    get => selectedPlayList;
    set => SetProperty(ref selectedPlayList, value);
}
```

Next, we need to implement the `IQueryAttributable` interface so we can receive the parameter value:

```c#
public class HomePageViewModel : BaseViewModel, IQueryAttributable
```

Now, let's add the method:

```c#
public void ApplyQueryAttributes(IDictionary<string, string> query)
{
    if (query.Count == 0)
    {
        SelectedPlayList = null;
        return;
    }

    string Id = HttpUtility.UrlDecode(query["PlayListId"]);
    if (Id != "")
    {
        var CacheDir = FileSystem.CacheDirectory + "/playlists";
        string FileName = $"{CacheDir}/{Id}.json";
        if (System.IO.File.Exists(FileName))
        {
            var json = System.IO.File.ReadAllText(FileName);
            SelectedPlayList = JsonConvert.DeserializeObject<PlayList>(json);
        }
    }
}
```

### Step 34: Implement the SELECT feature

To *HomePageViewModel.cs add the command for the **SELECT** button to execute:

```c#
private ICommand selectShowForPlayList;
public ICommand SelectShowForPlayList
{
    get
    {
        if (selectShowForPlayList == null)
        {
            selectShowForPlayList = new AsyncCommand<int>(PerformSelectShowForPlayList);
        }
        return selectShowForPlayList;
    }
}

public async Task PerformSelectShowForPlayList(int ShowId)
{
    await Task.Delay(0);
    if (SelectedPlayList == null) return;
    var show = (from x in SelectedPlayList.Shows
                where x.Id == ShowId select x).FirstOrDefault();
    if (show == null)
    {
        var addThis = (from x in AllShows where x.Id == ShowId select x).First();
        SelectedPlayList.Shows.Add(addThis);
        string json = JsonConvert.SerializeObject(SelectedPlayList);
        string FileName = $"{CacheDir}/{SelectedPlayList.Id}.json";
        System.IO.File.WriteAllText(FileName, json);
        base.OnPropertyChanged("SelectedPlayList");
    }
}
```

`PerformSelectShowForPlayList` checks to see if the show specified by `ShowId` is in the `SelectedPlayList.Shows` list. If not, it pulls the show from AllShows, adds it, and saves the `SelectedPlayList` to storage.

Next, we need to determine whether to show the **SELECT** and **REMOVE** buttons. Let's start with **SELECT**.

First, we'll need a `ValueConverter` to accept a `PlayList` and a `Show.Id` and return a bool whether the **SELECT** button should be visible.

Add the following class to the **MobileDnr** project:

*PlayListToSelectButtonVisibleConverter.cs*:

```c#
using DotNetRocks.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Xamarin.Forms;

namespace DotNetRocks
{
    public class PlayListToSelectButtonVisibleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) 
                // No Playlist
                return false;

            var playlist = (PlayList)value;

            // We're going to pass a label with the Text set to the Show.Id
            var label = (Label)parameter;

            if (label.Text == null) 
                // No value here. Unexpected, for sure
                return false;

            // Convert the Text property to an int
            int Id = System.Convert.ToInt32(label.Text);

            // Check to see if the show is in the playlist Shows list
            var match = (from x in playlist.Shows 
                         where x.Id == Id select x).FirstOrDefault();
            if (match == null)
                // Not there, so we can show the SELECT button
                return true;
            else
                // It's there. DON'T show the SELECT button
                return false;
        }

        public object ConvertBack(object value, Type targetType,
                                  object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
```

The comments tell all.

Why do we need to pass the `Show.Id` in the Text property of a `Label`?

It has to do with XAML binding. The short version is, XAML gets complicated when you need to do stuff with multiple binding contexts. This is a good example of why the Blazor Component Model is much easier than XAML. I'm looking forward to rewriting this as a MAUI app using Blazor as the UI. This little workaround took me hours to figure out. You'll see why in a few minutes.

#### Update the HomePage View

Add this resource to the top of the page:

```c#
<ContentPage.Resources>
    <local:PlayListToSelectButtonVisibleConverter 
        x:Key="PlayListToSelectButtonVisibleConverter"/>
</ContentPage.Resources>
```

Next, replace this:

```xaml
<Button Text="Episode Details"
        Command="{Binding GoToDetailsPage,
                 Source={RelativeSource 
                 AncestorType={x:Type viewmodels:HomePageViewModel}}}"
        CommandParameter="{Binding ShowNumber}" />
```

with this:

```xaml
<StackLayout Orientation="Horizontal">
    <Label x:Name="HiddenId" IsVisible="false" Text="{Binding Id}" />
    <Button Text="Select"
            Command="{Binding SelectShowForPlayList,
                     Source={RelativeSource 
                     AncestorType={x:Type viewmodels:HomePageViewModel}}}"
            CommandParameter ="{Binding Id}">
        <Button.IsVisible>
            <Binding Path="SelectedPlayList"
                     Converter="{StaticResource PlayListToSelectButtonVisibleConverter}"
                     ConverterParameter="{x:Reference HiddenId}"
                     Source="{RelativeSource 
                             AncestorType={x:Type viewmodels:HomePageViewModel}}">
            </Binding>
        </Button.IsVisible>
    </Button>
    <Button Text="Episode Details"
            Command="{Binding GoToDetailsPage,
                     Source={RelativeSource 
                     AncestorType={x:Type viewmodels:HomePageViewModel}}}"
            CommandParameter="{Binding ShowNumber}" />
</StackLayout>
```

Both buttons go into a Horizontal StackLayout

Here's the muck. I expanded the Button `IsVisible` binding into nested XAML so it's easier to follow.

We are Binding `IsVisible` to the `SelectedPlayList` property using the `PlayListToSelectButtonVisibleConverter` value converter. 

The kicker is that you can't bind the `ConverterParameter` directly. But we can use a reference to an existing XAML element. 

That's why I created this:

```xaml
<Label x:Name="HiddenId" IsVisible="false" Text="{Binding Id}" />
```

It's a messy workaround, yes. But, it works. 

We need one more thing. When we navigate to the `PlayListManagerPage` we need to tell the view to refresh the PlayList property so it will reload them from storage.

To *PlayListManagerPageViewModel.cs*, add the following:

```c#
public void RefreshPlayLists()
{
    playLists = new ObservableCollection<PlayList>();
    var playListJsonFiles = Directory.GetFiles(CacheDir, "*.json");
    foreach (var file in playListJsonFiles)
    {
        var json = System.IO.File.ReadAllText(file);
        var list = JsonConvert.DeserializeObject<PlayList>(json);
        playLists.Add(list);
    }
    base.OnPropertyChanged("PlayLists");
}
```

Now we can call this from the `OnAppearing()` method in *PlayListManagerPage.xaml.cs* :

```c#
protected override async void OnAppearing()
{
    await Task.Delay(0);
    var viewModel = (PlayListManagerPageViewModel)BindingContext;
    viewModel.Page = this;
    viewModel.RefreshPlayLists();
}
```

Give it a go!

<img src="ModalDialogs Images/image-20210730012321040.png" alt="image-20210730012321040" style="zoom:70%;" />

<img src="ModalDialogs Images/image-20210730012401542.png" alt="image-20210730012401542" style="zoom:70%;" />

Select **ADD/REMOVE**

<img src="ModalDialogs Images/image-20210730012438774.png" alt="image-20210730012438774" style="zoom:70%;" />

Select **SELECT** on the first episode

<img src="ModalDialogs Images/image-20210730012509613.png" alt="image-20210730012509613" style="zoom:70%;" />

Now, our `PlayList` should have one show in the collection. Press the BACK button in the top-left.

<img src="ModalDialogs Images/image-20210730012604685.png" alt="image-20210730012604685" style="zoom:70%;" />

Now, if you navigate Home with the hamburger menu on the top-left, none of the shows will display a **SELECT** button:

<img src="ModalDialogs Images/image-20210730012727655.png" alt="image-20210730012727655" style="zoom:70%;" />



### Step 35: Implement the REMOVE feature

To *HomePageViewModel.cs*, add the command for the **REMOVE** button to execute:

```c#
private ICommand removeShowFromPlayList;
public ICommand RemoveShowFromPlayList
{
    get
    {
        if (removeShowFromPlayList == null)
        {
            removeShowFromPlayList = 
                new AsyncCommand<int>(PerformRemoveShowFromPlayList);
        }
        return removeShowFromPlayList;
    }
}

public async Task PerformRemoveShowFromPlayList(int ShowId)
{
    await Task.Delay(0);
    if (SelectedPlayList == null) return;
    var show = (from x in SelectedPlayList.Shows 
                where x.Id == ShowId select x).FirstOrDefault();
    if (show != null)
    {
        SelectedPlayList.Shows.Remove(show);
        string json = JsonConvert.SerializeObject(SelectedPlayList);
        string FileName = $"{CacheDir}/{SelectedPlayList.Id}.json";
        System.IO.File.WriteAllText(FileName, json);
        base.OnPropertyChanged("SelectedPlayList");
    }
}
```

`PerformRemoveShowFromPlayList` checks to see if the show specified by `ShowId` is in the `SelectedPlayList.Shows` list. If so, it removes the show from `SelectedPlayList` and writes the `PlayList` to storage.

Next, we need to determine whether to show the **REMOVE** button. 

As before, we'll need a `ValueConverter` to accept a `PlayList` and a `Show.Id` and return a bool whether the **REMOVE** button should be visible.

Add the following class to the **MobileDnr** project:

*PlayListToRemoveButtonVisibleConverter.cs*:

```c#
using DotNetRocks.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Xamarin.Forms;

namespace DotNetRocks
{
    public class PlayListToRemoveButtonVisibleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, 
                              object parameter, CultureInfo culture)
        {
            if (value == null)
                // No Playlist
                return false;

            var playlist = (PlayList)value;

            // We're going to pass a label with the Text set to the Show.Id
            var label = (Label)parameter;

            if (label.Text == null)
                // No value here. Unexpected, for sure
                return false;

            // Convert the Text property to an int
            int Id = System.Convert.ToInt32(label.Text);

            // Check to see if the show is in the playlist Shows list
            var match = (from x in playlist.Shows 
                         where x.Id == Id select x).FirstOrDefault();
            if (match != null)
                // It's there, so we can show the REMOVE button
                return true;
            else
                // It's not there. DON'T show the REMOVE button
                return false;
        }

        public object ConvertBack(object value, Type targetType, 
                                  object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

```

#### Update the HomePage View

Add the converter to the resources at the top of the page:

```c#
<ContentPage.Resources>
    <local:PlayListToSelectButtonVisibleConverter 
        x:Key="PlayListToSelectButtonVisibleConverter"/>
	<local:PlayListToRemoveButtonVisibleConverter
        x:Key="PlayListToRemoveButtonVisibleConverter"/>
</ContentPage.Resources>
```

Next, add the **REMOVE** button to the `StackLayout` just after the **SELECT** button:

```xaml
<Button Text="Remove"
        Command="{Binding RemoveShowFromPlayList,
                 Source={RelativeSource 
                 AncestorType={x:Type viewmodels:HomePageViewModel}}}"
        CommandParameter ="{Binding Id}">
    <Button.IsVisible>
        <Binding Path="SelectedPlayList"
                 Converter="{StaticResource PlayListToRemoveButtonVisibleConverter}"
                 ConverterParameter="{x:Reference HiddenId}"
                 Source="{RelativeSource 
                         AncestorType={x:Type viewmodels:HomePageViewModel}}">
        </Binding>
    </Button.IsVisible>
</Button>
```

Test out the **SELECT** and **REMOVE** features. 

That's where we'll leave it today.
