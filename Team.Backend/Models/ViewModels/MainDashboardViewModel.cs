using System;
using System.Collections.Generic;

namespace Team.Backend.Models.ViewModels
{
    /// <summary>
    /// �D����O���ϼҫ��A�]�t�U�زέp�ƾ�
    /// </summary>
    public class MainDashboardViewModel
    {
        #region �򥻲έp�ƾ�
        
        // �q�����
        public int TotalOrders { get; set; }
        public int NewOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        
        // �|������
        public int TotalMembers { get; set; }
        public int NewMembers { get; set; }
        
        // �ӫ~����
        public int TotalProducts { get; set; }
        public int LowStockProducts { get; set; }
        
        // �q������
        public int TotalNotifications { get; set; }
        
        // �峹����
        public int TotalArticles { get; set; }
        
        // �u�f�����
        public int TotalCoupons { get; set; }

        // �s�i�`��
        public int TotalAds { get; set; }
        
        #endregion
        
        #region �L�o����
        
        // ����d��
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        
        #endregion
        
        #region �Ϫ�ƾ�
        
        // �P��Ϫ�ƾ� - �@�뤣�|������J�A�ϥ� AJAX �ШD
        public object SalesChartData { get; set; }
        
        // ��������ƾ� - �@�뤣�|������J�A�ϥ� AJAX �ШD
        public object CategoryDistributionData { get; set; }
        
        #endregion
        
        #region �C��ƾ�
        
        // �̪�q��C�� - �@�뤣�|������J�A�ϥ� AJAX �ШD
        public List<object> RecentOrders { get; set; }
        
        // �����ӫ~�C�� - �@�뤣�|������J�A�ϥ� AJAX �ШD
        public List<object> PopularProducts { get; set; }
        
        #endregion
        
        public MainDashboardViewModel()
        {
            // ��l�ƦC��
            RecentOrders = new List<object>();
            PopularProducts = new List<object>();
        }
    }
}