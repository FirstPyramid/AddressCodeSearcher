using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace AddressCode
{
	/// <summary>
	/// MainWindow.xaml에 대한 상호 작용 논리
	/// </summary>
	public partial class MainWindow : Window
	{
		readonly CodeDictionary codeDict;
		public MainWindow()
		{
			InitializeComponent();

			// OpenFileDialog 초기화
			OpenFileDialog ofd = new OpenFileDialog
			{
				// txt 파일만 읽어올 수 있음
				Filter = "법정동코드 파일 (*.txt)|*.txt",

				// 기본 폴더는 내 문서
				InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
			};

			// 프로그램 실행시 파일 열기 창 띄우기
			// 파일을 무사히 열었으면 CodeDictionary로 파일 넘겨서 코드 분석 및 저장
			if (ofd.ShowDialog() == true)
			{
				codeDict = new CodeDictionary(ofd.FileName);
			}
			// 못열면 프로그램 종료
			else
			{
				if (MessageBox.Show("파일 열기에 실패했습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Warning) == MessageBoxResult.OK)
					Close();
			}
		}

		private void SearchButton_Clicked(object sender, RoutedEventArgs e)
		{

			// 입력값이 정수인지 확인
			bool isNumber = long.TryParse(TextInput.Text, out long inputNum);

			// 입력값이 정수인 경우
			if (isNumber)
			{
				// 입력 코드가 10자리면 검색
				if(TextInput.Text.Length == 10)
				{
					// 검색 결과를 결과창에 저장
					TextOutput.Text = codeDict.SearchAddress(inputNum);
				}
				// 입력 코드가 10자리가 아닐 경우 오류창
				else
				{
					MessageBox.Show("잘못된 코드입니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
				}
			}
			// 입력값이 정수가 아닌 경우
			else
			{
				/*
				 * 체크박스 값을 정수 하나로 정리
				 * 시·도: 8, 시·군·구: 4, 읍·면·동: 2, 리: 1
				 * 합쳐서 변수로 전달
				 */
				int searchOption = 0;

				if (CheckBox_Sido.IsChecked == true)
					searchOption += 8;
				if (CheckBox_SGG.IsChecked == true)
					searchOption += 4;
				if (CheckBox_EMD.IsChecked == true)
					searchOption += 2;
				if (CheckBox_Ri.IsChecked == true)
					searchOption += 1;

				// 입력 주소를 포함하는 법정동명을 검색
				List<string> results = codeDict.SearchCode(TextInput.Text, searchOption);

				// 모든 결과를 string 하나로 이어 출력
				StringBuilder sb = new StringBuilder();
				foreach (string result in results)
					sb.Append(result + "\n");

				TextOutput.Text = sb.ToString();
			}
		}
	}
}
