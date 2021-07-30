using System;
using System.Collections.Generic;
using System.Windows.Input;
using System.Threading.Tasks;
using System.Linq;
using Xamarin.Forms;
using MvvmHelpers;
using MvvmHelpers.Commands;
using DotNetRocks.Services;
using DotNetRocks.Models;
using DotNetRocks.Views;
using System.Collections.ObjectModel;
using System.Web;

using Xamarin.Essentials;
using MonkeyCache.FileStore;
using Newtonsoft.Json;

namespace DotNetRocks.ViewModels
{
    public class HomePageViewModel : BaseViewModel, IQueryAttributable
    {
        private ApiService ApiService = new ApiService();
        public List<int> ShowNumbers { get; set; } = new List<int>();
        public int RecordsToRead { get; set; } = 20;
        public int LastShowNumber { get; set; }
        private string CacheDir = "";

        public HomePageViewModel()
        {
            Barrel.ApplicationId = "mobile_dnr";
            CacheDir = FileSystem.CacheDirectory + "/playlists";
            var t = Task.Run(() => GetNextBatchOfShows());
            t.Wait();
        }

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
                // GetNextBatchOfShows will execute a filter
                var t = Task.Run(() => GetNextBatchOfShows());
                t.Wait();
                base.OnPropertyChanged("EpisodeFilter");
            }
        }

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

        public async Task GetNextBatchOfShows()
        {
            if (EpisodeFilter != "")
            {
                AllShows.Clear();
                await GetNextBatchOfFilteredShows();
                return;
            }

            if (ShowNumbers.Count == 0)
            {
                ShowNumbers = await ApiService.GetShowNumbers();
                if (ShowNumbers == null || ShowNumbers.Count == 0) return;
                LastShowNumber = ShowNumbers.First<int>() + 1;
            }

            var request = new GetByShowNumbersRequest()
            {
                ShowName = "dotnetrocks",
                Indexes = (from x in ShowNumbers where x < LastShowNumber && x > (LastShowNumber - RecordsToRead) select x).ToList()
            };

            var nextBatch = await ApiService.GetByShowNumbers(request);
            if (nextBatch == null || nextBatch.Count == 0) return;

            foreach (var show in nextBatch)
            {
                AllShows.Add(show);
            }

            LastShowNumber = nextBatch.Last<Show>().ShowNumber;
            base.OnPropertyChanged("AllShows");
        }

        public async Task NavigateToDetailPage(int ShowNumber)
        {
            var route = $"{nameof(DetailPage)}?ShowNumber={ShowNumber}";
            await Shell.Current.GoToAsync(route);
        }

        private PlayList selectedPlayList = null;
        public PlayList SelectedPlayList
        {
            get => selectedPlayList;
            set => SetProperty(ref selectedPlayList, value);
        }

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
                
                string FileName = $"{CacheDir}/{Id}.json";
                if (System.IO.File.Exists(FileName))
                {
                    var json = System.IO.File.ReadAllText(FileName);
                    SelectedPlayList = JsonConvert.DeserializeObject<PlayList>(json);
                }
            }
        }

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
            var show = (from x in SelectedPlayList.Shows where x.Id == ShowId select x).FirstOrDefault();
            if (show != null)
            {
                SelectedPlayList.Shows.Add(show);
                string json = JsonConvert.SerializeObject(SelectedPlayList);
                string FileName = $"{CacheDir}/{SelectedPlayList.Id}.json";
                System.IO.File.WriteAllText(FileName, json);
                base.OnPropertyChanged("SelectedPlayList");
            }
        }

        private ICommand removeShowFromPlayList;
        public ICommand RemoveShowFromPlayList
        {
            get
            {
                if (removeShowFromPlayList == null)
                {
                    removeShowFromPlayList = new AsyncCommand<int>(PerformRemoveShowFromPlayList);
                }
                return removeShowFromPlayList;
            }
        }

        public async Task PerformRemoveShowFromPlayList(int ShowId)
        {
            await Task.Delay(0);
            if (SelectedPlayList == null) return;
            var show = (from x in SelectedPlayList.Shows where x.Id == ShowId select x).FirstOrDefault();
            if (show != null)
            {
                SelectedPlayList.Shows.Remove(show);
                string json = JsonConvert.SerializeObject(SelectedPlayList);
                string FileName = $"{CacheDir}/{SelectedPlayList.Id}.json";
                System.IO.File.WriteAllText(FileName, json);
                base.OnPropertyChanged("SelectedPlayList");
            }
        }

        private ICommand goToDetailsPage;
        public ICommand GoToDetailsPage
        {
            get
            {
                if (goToDetailsPage == null)
                {
                    goToDetailsPage = new AsyncCommand<int>(NavigateToDetailPage);
                }
                return goToDetailsPage;
            }
        }

        private ICommand loadMoreShows;
        public ICommand LoadMoreShows
        {
            get
            {
                if (loadMoreShows == null)
                {
                    loadMoreShows = new AsyncCommand(GetNextBatchOfShows);
                }
                return loadMoreShows;
            }
        }

        private ObservableCollection<Show> allShows = new ObservableCollection<Show>();
        public ObservableCollection<Show> AllShows
        {
            get => allShows;
            set => SetProperty(ref allShows, value);
        }

    }
}